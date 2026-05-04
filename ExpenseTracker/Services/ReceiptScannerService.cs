using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using ExpenseTracker.Models;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions; // Dodane dla odkurzacza Regex

namespace ExpenseTracker.Services
{
    public class ReceiptScannerService : IReceiptScannerService
    {
        private readonly string _azureEndpoint;
        private readonly string _azureApiKey;
        private readonly string _openAiApiKey;
        private readonly HttpClient _httpClient;

        public ReceiptScannerService(IConfiguration configuration, HttpClient httpClient)
        {
            _azureEndpoint = configuration["AzureDocumentIntelligence:Endpoint"] ?? "";
            _azureApiKey = configuration["AzureDocumentIntelligence:ApiKey"] ?? "";
            _openAiApiKey = configuration["OpenAI:ApiKey"] ?? "";
            _httpClient = httpClient;
        }

        public async Task<ScannedReceiptDto?> ScanReceiptAsync(Stream imageStream)
        {
            // --- 1. ETAP: AZURE (Nasz własny wytrenowany model Neural) ---
            var credential = new AzureKeyCredential(_azureApiKey);
            var client = new DocumentAnalysisClient(new Uri(_azureEndpoint), credential);

            // TUTAJ WPISZ ID SWOJEGO MODELU Z AZURE STUDIO (Zamiast ModelParagony)
            string myCustomModelId = "ModelParagony";

            AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, myCustomModelId, imageStream);
            AnalyzedDocument? receipt = operation.Value.Documents.FirstOrDefault();

            if (receipt == null) return null;

            var dto = new ScannedReceiptDto
            {
                MerchantName = GetFieldValue(receipt, "MerchantName") ?? "Nieznany sklep",
                // Zabezpieczony pobór sumy
                TotalAmount = GetDecimalValue(receipt, "TotalAmount"),
                TransactionDate = DateTime.Today // Możesz dodać datę do modelu w przyszłości
            };

            // --- 2. ETAP: Wyciągnięcie CZYSTYCH DANYCH z naszej tabeli i Matematyka (C#) ---
            var rawItems = new List<RawReceiptItem>();

            if (receipt.Fields.TryGetValue("Items", out DocumentField? itemsField) && itemsField.FieldType == DocumentFieldType.List)
            {
                foreach (var itemField in itemsField.Value.AsList())
                {
                    var itemDict = itemField.Value.AsDictionary();

                    string name = GetDictionaryStringValue(itemDict, "Name") ?? "Nieznany produkt";

                    // Używamy naszego bezpiecznego odkurzacza do wyciągania kwot z literek
                    decimal basePrice = GetDictionaryDecimalValue(itemDict, "BasePrice") ?? 0m;
                    decimal discount = GetDictionaryDecimalValue(itemDict, "Discount") ?? 0m;

                    // Ostateczna matematyka po stronie programu!
                    decimal finalPrice = basePrice + discount;

                    if (finalPrice != 0 && name != "Nieznany produkt")
                    {
                        rawItems.Add(new RawReceiptItem { Name = name, Price = finalPrice });
                    }
                }
            }

            // --- 3. ETAP: OPENAI (Wyłącznie Kategoryzacja gotowych danych) ---
            if (rawItems.Any())
            {
                var processedCategories = await ProcessReceiptWithOpenAIAsync(rawItems);

                if (processedCategories != null)
                {
                    // --- 4. ETAP: POST-PROCESSING (Grupowanie ostateczne) ---
                    var groupedCategories = new List<SubCategorySummaryDto>();

                    foreach (var item in processedCategories)
                    {
                        var existingGroup = groupedCategories.FirstOrDefault(g =>
                            g.Category == item.Category &&
                            g.SubCategory == item.SubCategory);

                        if (existingGroup != null)
                        {
                            existingGroup.Amount += item.Amount;
                            if (item.ItemNames != null && item.ItemNames.Any())
                            {
                                existingGroup.ItemNames.AddRange(item.ItemNames);
                            }
                        }
                        else
                        {
                            groupedCategories.Add(new SubCategorySummaryDto
                            {
                                Category = item.Category,
                                SubCategory = item.SubCategory,
                                Amount = item.Amount,
                                ItemNames = item.ItemNames ?? new List<string>()
                            });
                        }
                    }

                    dto.SubCategories = groupedCategories;
                }
            }

            return dto;
        }

        // Klasy pomocnicze
        private class RawReceiptItem
        {
            public string Name { get; set; }
            public decimal Price { get; set; }
        }

        private class OpenAiReceiptResponse
        {
            public string Rozumowanie { get; set; }
            public List<SubCategorySummaryDto> Wynik { get; set; }
        }

        private async Task<List<SubCategorySummaryDto>?> ProcessReceiptWithOpenAIAsync(List<RawReceiptItem> rawItems)
        {
            if (string.IsNullOrEmpty(_openAiApiKey)) return null;

            string jsonList = JsonSerializer.Serialize(rawItems);
            System.Diagnostics.Debug.WriteLine("\n=== CZYSTA LISTA DO AI ===");
            System.Diagnostics.Debug.WriteLine(jsonList);

            // Zmniejszony prompt - OpenAI dostaje gotowe ceny, musi je tylko ułożyć w szufladki
            string systemPrompt = @"
Jesteś asystentem finansowym. Otrzymujesz czystą listę produktów wraz z ich ostatecznymi cenami, przygotowaną przez program.

TWOJE ZADANIE:
1. Przypisz KAŻDY otrzymany produkt do jednej z kategorii i podkategorii:
""Zakupy spożywcze"": [""Mięso"", ""Nabiał"", ""Pieczywo"", ""Warzywa"",""Owoce"",""Słodycze i Przekąski"", ""Napoje"", ""Napoje energetyczne"", ""artykuły suche"", ""Tłuszcze"",""Sosy i Syropy"",""Przyprawy i Słodziki"", ""Dania Gotowe""],
""Transport"": [""Paliwo"", ""Bilety Komunikacji Miejskiej"", ""Taksówki/Uber"", ""Serwis Auta"", ""Ubezpieczenie Auta"", ""Przegląd"", ""Bilety lotnicze"", ""Bilety PKP"", ""Nocleg""],
""Media"": [""Czynsz"", ""Prąd"", ""Internet"", ""Woda"", ""Gaz"", ""Telefon""],
""Chemia"": [""Środki czystości""],
""Rozrywka"": [""Gry"", ""Serwisy Streamingowe"", ""Wyjścia""],
""Zdrowie"": [""Lekarze"", ""Leki"", ""Suplementy"", ""Sport""],
""Kosmetyki"": [""Do Twarzy"", ""Do Ciała"", ""Do Włosów"", ""Makijaż""],
""Inne"": [""Karma dla zwierząt""]

Zwróć wynik BEZWZGLĘDNIE jako obiekt JSON (bez znaczników ```json) ze strukturą:
{
  ""Rozumowanie"": ""Krótko potwierdź przypisanie kategorii."",
  ""Wynik"": [
    { ""Category"": ""Zakupy spożywcze"", ""SubCategory"": ""Mięso"", ""Amount"": 28.32, ""ItemNames"": [""Filet z kurczaka""] }
  ]
}";

            var requestBody = new
            {
                model = "gpt-4o-mini",
                temperature = 0.0,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = jsonList }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Add("Authorization", $"Bearer {_openAiApiKey}");
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                var responseString = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(responseString);
                string aiContent = jsonDoc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";

                aiContent = aiContent.Replace("```json", "").Replace("```", "").Trim();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var responseObj = JsonSerializer.Deserialize<OpenAiReceiptResponse>(aiContent, options);

                return responseObj?.Wynik;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd AI: {ex.Message}");
                return null;
            }
        }

        // --- Metody pomocnicze (Pola luźne np. MerchantName, TotalAmount) ---
        private string? GetFieldValue(AnalyzedDocument doc, string fieldName)
        {
            if (doc.Fields.TryGetValue(fieldName, out DocumentField? field) && field.FieldType == DocumentFieldType.String)
                return field.Value.AsString();
            return null;
        }

        private decimal? GetDecimalValue(AnalyzedDocument doc, string fieldName)
        {
            if (doc.Fields.TryGetValue(fieldName, out DocumentField? field))
            {
                if (field.FieldType == DocumentFieldType.Double) return (decimal)field.Value.AsDouble();
                if (field.FieldType == DocumentFieldType.String) return CleanAndParseDecimal(field.Value.AsString());
            }
            return null;
        }

        // --- Metody pomocnicze (Słownik wewnątrz tabeli np. Name, BasePrice) ---
        private string? GetDictionaryStringValue(IReadOnlyDictionary<string, DocumentField> dict, string key)
        {
            if (dict.TryGetValue(key, out DocumentField? field) && field.FieldType == DocumentFieldType.String)
                return field.Value.AsString();
            return null;
        }

        private decimal? GetDictionaryDecimalValue(IReadOnlyDictionary<string, DocumentField> dict, string key)
        {
            if (dict.TryGetValue(key, out DocumentField? field))
            {
                if (field.FieldType == DocumentFieldType.Double) return (decimal)field.Value.AsDouble();
                if (field.FieldType == DocumentFieldType.String) return CleanAndParseDecimal(field.Value.AsString());
            }
            return null;
        }

        // --- Super bezpieczny odkurzacz dla kwot ---
        private decimal? CleanAndParseDecimal(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return null;

            // Zostawia tylko cyfry, minus i kropkę/przecinek
            string cleanedText = Regex.Replace(rawText, "[^0-9.,-]", "").Replace(",", ".");

            if (decimal.TryParse(cleanedText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal result))
            {
                return result;
            }
            return null;
        }
    }
}