using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using ExpenseTracker.Models;
using System.Text;
using System.Text.Json;

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
            //czysty ocr
            var credential = new AzureKeyCredential(_azureApiKey);
            var client = new DocumentAnalysisClient(new Uri(_azureEndpoint), credential);

            AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-receipt", imageStream);
            AnalyzedDocument? receipt = operation.Value.Documents.FirstOrDefault();

            if (receipt == null) return null;

            var dto = new ScannedReceiptDto
            {
                MerchantName = GetFieldValue(receipt, "MerchantName") ?? "Nieznany sklep",
                TotalAmount = GetDecimalValue(receipt, "Total"),
                TransactionDate = GetDateValue(receipt, "TransactionDate") ?? DateTime.Today
            };

            // Pobieram cały tekst
            string rawReceiptText = operation.Value.Content;

            // analliza, dedukcja, kategoryzacja
            if (!string.IsNullOrWhiteSpace(rawReceiptText))
            {
                var processedCategories = await ProcessReceiptWithOpenAIAsync(rawReceiptText);

                if (processedCategories != null)
                {
                    //ostateczne grupowanie
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

        // Klasa pomocnicza do mapowania odpowiedzi AI z polem "Rozumowanie"
        private class OpenAiReceiptResponse
        {
            public string Rozumowanie { get; set; }
            public List<SubCategorySummaryDto> Wynik { get; set; }
        }

        private async Task<List<SubCategorySummaryDto>?> ProcessReceiptWithOpenAIAsync(string rawText)
        {
            if (string.IsNullOrEmpty(_openAiApiKey)) return null;

            System.Diagnostics.Debug.WriteLine("\n=== SUROWY TEKST WYSŁANY DO AI ===");
            System.Diagnostics.Debug.WriteLine(rawText);

            string systemPrompt = @"
Jesteś wybitnym detektywem finansowym. Otrzymujesz surowy tekst z polskiego paragonu zrzucony przez OCR.

BŁĘDY SKANERA (BARDZO WAŻNE):
Tekst z OCR jest poszarpany, a ceny bardzo często są PRZESUNIĘTE W DÓŁ o 1 lub 2 pozycje względem nazwy produktu! 
Zauważ logikę np.:
- Jeśli pod 'Winogrona' widzisz '1 x 3,99', to to NIE jest cena winogron (winogrona są na wagę!). To zgubiona cena produktu wyżej (np. Syropu).
- Cena winogron ('0,216 x 19,99 = 4,32') znajduje się pewnie jeszcze niżej, np. przy słowie 'Podsuma'.
Kieruj się logiką (mnożniki, wagi, odliczenia opustów), a nie tylko bliskością tekstu.

TWOJE ZADANIE:
1. Zidentyfikuj WSZYSTKIE produkty (nie pomiń niczego).
2. Dopasuj do nich właściwe ceny bazowe i uwzględnij opusty leżące pod nimi.
3. Przypisz produkty do kategorii: 
""Zakupy spożywcze"": [""Mięso"", ""Nabiał"", ""Pieczywo"", ""Warzywa"",""Owoce"",""Słodycze i Przekąski"", ""Napoje"", ""Napoje energetyczne"", ""artykuły suche"", ""Tłuszcze"",""Sosy i Syropy"",""Przyprawy i Słodziki"", ""Dania Gotowe""],
""Transport"": [""Paliwo"", ""Bilety Komunikacji Miejskiej"", ""Taksówki/Uber"", ""Serwis Auta"", ""Ubezpieczenie Auta"", ""Przegląd"", ""Bilety lotnicze"", ""Bilety PKP"", ""Nocleg""],
""Media"": [""Czynsz"", ""Prąd"", ""Internet"", ""Woda"", ""Gaz"", ""Telefon""],
""Chemia"": [""Środki czystości""],
""Rozrywka"": [""Gry"", ""Serwisy Streamingowe"", ""Wyjścia""],
""Zdrowie"": [""Lekarze"", ""Leki"", ""Suplementy"", ""Sport""],
""Kosmetyki"": [""Do Twarzy"", ""Do Ciała"", ""Do Włosów"", ""Makijaż""],
""Inne"": []

Zwróć wynik BEZWZGLĘDNIE jako obiekt JSON (bez znaczników ```json) ze strukturą:
{
  ""Rozumowanie"": ""Tutaj krok po kroku napisz, jak połączyłeś ceny z produktami, np. Syrop to 3.99, a winogrona to 4.32."",
  ""Wynik"": [
    { ""Category"": ""Zakupy spożywcze"", ""SubCategory"": ""Sosy i Syropy"", ""Amount"": 3.99, ""ItemNames"": [""Syrop ZERO CUKRU""] }
  ]
}";

            var requestBody = new
            {
                model = "gpt-4o-mini",
                temperature = 0.0,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = rawText }
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

                System.Diagnostics.Debug.WriteLine("\n=== CO ZWRÓCIŁO AI ===");
                System.Diagnostics.Debug.WriteLine(aiContent);

                aiContent = aiContent.Replace("```json", "").Replace("```", "").Trim();

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var responseObj = JsonSerializer.Deserialize<OpenAiReceiptResponse>(aiContent, options);

                return responseObj?.Wynik;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd C#: {ex.Message}");
                return null;
            }
        }

        // --- Metody pomocnicze ---
        private string? GetFieldValue(AnalyzedDocument doc, string fieldName)
        {
            if (doc.Fields.TryGetValue(fieldName, out DocumentField? field) && field.FieldType == DocumentFieldType.String)
                return field.Value.AsString();
            return null;
        }

        private decimal? GetDecimalValue(AnalyzedDocument doc, string fieldName)
        {
            if (doc.Fields.TryGetValue(fieldName, out DocumentField? field) && field.FieldType == DocumentFieldType.Double)
                return (decimal)field.Value.AsDouble();
            return null;
        }

        private DateTime? GetDateValue(AnalyzedDocument doc, string fieldName)
        {
            if (doc.Fields.TryGetValue(fieldName, out DocumentField? field) && field.FieldType == DocumentFieldType.Date)
                return field.Value.AsDate().DateTime;
            return null;
        }
    }
}