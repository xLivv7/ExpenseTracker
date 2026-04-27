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
            // --- 1. ETAP: AZURE (Czytanie obrazu) ---
            var credential = new AzureKeyCredential(_azureApiKey);
            var client = new DocumentAnalysisClient(new Uri(_azureEndpoint), credential);

            AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-receipt", imageStream);
            AnalyzedDocument? receipt = operation.Value.Documents.FirstOrDefault();

            if (receipt == null) return null;

            var dto = new ScannedReceiptDto
            {
                MerchantName = GetFieldValue(receipt, "MerchantName"),
                // Pobieramy podsumę z Azure, ale finalnie i tak oprzemy się na wyliczeniach OpenAI
                TotalAmount = GetDecimalValue(receipt, "Total"),
                TransactionDate = GetDateValue(receipt, "TransactionDate") ?? DateTime.Today
            };

            // --- 2. ETAP: Wyciągnięcie SUROWYCH produktów i opustów (Nawet ujemnych) ---
            // Zamiast tylko nazw, tworzymy listę obiektów anonimowych z nazwą i ceną
            var rawItems = new List<object>();

            if (receipt.Fields.TryGetValue("Items", out DocumentField? itemsField) && itemsField.FieldType == DocumentFieldType.List)
            {
                foreach (var itemField in itemsField.Value.AsList())
                {
                    var itemDict = itemField.Value.AsDictionary();
                    string description = GetDictionaryStringValue(itemDict, "Description") ?? "Nieznany";
                    decimal price = GetDictionaryDecimalValue(itemDict, "TotalPrice") ?? 0m;

                    // Pobieramy wszystko, co nie jest zerem (w tym ujemne opusty!)
                    if (price != 0 && description != "Nieznany")
                    {
                        rawItems.Add(new { Name = description, Price = price });
                    }
                }
            }

            // --- 3. ETAP: OPENAI (Naprawa, Matematyka i Kategoryzacja) ---
            if (rawItems.Any())
            {
                // Wysyłamy do OpenAI całą strukturę z cenami
                var processedCategories = await ProcessReceiptWithOpenAIAsync(rawItems);

                if (processedCategories != null)
                {
                    dto.SubCategories = processedCategories;
                }
            }

            return dto;
        }

        // Klasa pomocnicza - musi być zdefiniowana nad lub pod metodą w tym samym pliku
        private class OpenAiReceiptResponse
        {
            public string Rozumowanie { get; set; }
            public List<SubCategorySummaryDto> Wynik { get; set; }
        }

        private async Task<List<SubCategorySummaryDto>?> ProcessReceiptWithOpenAIAsync(List<object> rawItems)
        {
            if (string.IsNullOrEmpty(_openAiApiKey)) return null;

            string jsonList = JsonSerializer.Serialize(rawItems);

            // Nowy prompt używający techniki "Chain of Thought"
            string systemPrompt = @"
Jesteś zaawansowanym asystentem finansowym. Otrzymujesz dane OCR z polskiego paragonu (zachowana kolejność z wydruku).

ZASADY:
1. Słowo 'OPUST' lub 'RABAT' oznacza zniżkę, która ZAWSZE dotyczy produktu znajdującego się na liście tuż przed nią.
2. Twoim zadaniem jest odjęcie wartości opustu od ceny produktu (zignoruj fakt, czy opust ma znak minus czy plus, po prostu odejmij kwotę zniżki od kwoty produktu).
3. Pomiń śmieci z OCR (dziwne losowe ciągi znaków).

KATEGORIE DO WYBORU:
Zakupy spożywcze (Nabiał, Mięso, Pieczywo, Warzywa, Owoce, Słodycze i Przekąski, Napoje, Produkty suche/sypkie, Tłuszcze, Dania Gotowe), Transport (Paliwo, Bilety, Serwis Auta), Media, Chemia (Środki czystości), Zdrowie, Kosmetyki, Inne.

Zwróć wynik BEZWZGLĘDNIE jako czysty JSON w następującym formacie (nie używaj ```json):
{
  ""Rozumowanie"": ""TUTAJ opisz krok po kroku swoje obliczenia. Np. '1. DZIK Napój: 10.98, pod nim opust 5.50. Wynik: 5.48. 2. Krem proteinowy: 8.99, opust 4.50. Wynik: 4.49...'"",
  ""Wynik"": [
    {
      ""Category"": ""Zakupy spożywcze"",
      ""SubCategory"": ""Napoje"",
      ""Amount"": 5.48,
      ""ItemNames"": [""DZIK Napój energ.""]
    }
  ]
}";

            var requestBody = new
            {
                model = "gpt-4o-mini",
                temperature = 0.0, // 0 oznacza maksymalną dokładność i brak kreatywności (wymagane przy matematyce)
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

                // Dodano podgląd rozumowania w konsoli Visual Studio!
                System.Diagnostics.Debug.WriteLine($"\n--- MYŚLENIE AI ---\n{aiContent}\n-------------------\n");

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<OpenAiReceiptResponse>(aiContent, options);

                return result?.Wynik; // Zwracamy tylko właściwą tablicę z wynikami
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

        private string? GetDictionaryStringValue(IReadOnlyDictionary<string, DocumentField> dict, string key)
        {
            if (dict.TryGetValue(key, out DocumentField? field) && field.FieldType == DocumentFieldType.String)
                return field.Value.AsString();
            return null;
        }

        private decimal? GetDictionaryDecimalValue(IReadOnlyDictionary<string, DocumentField> dict, string key)
        {
            if (dict.TryGetValue(key, out DocumentField? field) && field.FieldType == DocumentFieldType.Double)
                return (decimal)field.Value.AsDouble();
            return null;
        }
    }
}