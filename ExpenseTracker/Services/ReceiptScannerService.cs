using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using ExpenseTracker.Models;

namespace ExpenseTracker.Services
{
    public class ReceiptScannerService : IReceiptScannerService
    {
        private readonly string _endpoint;
        private readonly string _apiKey;

        public ReceiptScannerService(IConfiguration configuration)
        {
            _endpoint = configuration["AzureDocumentIntelligence:Endpoint"] ?? "";
            _apiKey = configuration["AzureDocumentIntelligence:ApiKey"] ?? "";
        }

        public async Task<ScannedReceiptDto?> ScanReceiptAsync(Stream imageStream)
        {
            if (string.IsNullOrEmpty(_endpoint) || string.IsNullOrEmpty(_apiKey))
                throw new InvalidOperationException("Brak konfiguracji Azure AI w secrets.json lub appsettings.json");

            var credential = new AzureKeyCredential(_apiKey);
            var client = new DocumentAnalysisClient(new Uri(_endpoint), credential);

            try
            {
                // 1. Wysyłamy zdjęcie do Azure AI (model "prebuilt-receipt")
                AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-receipt", imageStream);
                AnalyzedDocument? receipt = operation.Value.Documents.FirstOrDefault();

                if (receipt == null) return null;

                // 2. Wyciągamy surowe dane z AI
                var dto = new ScannedReceiptDto
                {
                    MerchantName = GetFieldValue(receipt, "MerchantName"),
                    TotalAmount = GetDecimalValue(receipt, "Total"),
                    TransactionDate = GetDateValue(receipt, "TransactionDate") ?? DateTime.Now,
                    Description = "Skan paragonu"
                };

                // 3. Logika "Zgadywania Kategorii" na podstawie nazwy sklepu
                if (!string.IsNullOrEmpty(dto.MerchantName))
                {
                    dto.Description = $"Paragon: {dto.MerchantName}";
                    ApplyCategoryLogic(dto);
                }

                return dto;
            }
            catch (Exception ex)
            {
                // Tutaj można dodać logging (np. Serilog), jeśli go używasz
                return null;
            }
        }

        private void ApplyCategoryLogic(ScannedReceiptDto dto)
        {
            string name = dto.MerchantName!.ToLower();

            // Szybkie mapowanie - możesz tu dodać dowolną ilość sklepów
            if (ContainsAny(name, "biedronka", "lidl", "kaufland", "auchan", "dino", "żabka", "stokrotka"))
            {
                dto.Category = "Zakupy spożywcze";
                dto.SubCategory = "Artykuły domowe";
            }
            else if (ContainsAny(name, "orlen", "bp", "shell", "circle", "lotos"))
            {
                dto.Category = "Transport";
                dto.SubCategory = "Paliwo";
            }
            else if (ContainsAny(name, "rossmann", "hebe", "super-pharm"))
            {
                dto.Category = "Zdrowie i uroda";
                dto.SubCategory = "Kosmetyki";
            }
            else if (ContainsAny(name, "mcdonald", "kfc", "burger king", "pizzeria", "restauracja"))
            {
                dto.Category = "Jedzenie poza domem";
                dto.SubCategory = "Restauracje";
            }
            else if (ContainsAny(name, "leroy", "castorama", "obi", "ikea"))
            {
                dto.Category = "Dom i ogród";
                dto.SubCategory = "Remont";
            }
            else
            {
                // Jeśli nie rozpoznamy sklepu, zostawiamy puste - użytkownik sam wybierze
                dto.Category = "Inne";
            }
        }

        // Metody pomocnicze do bezpiecznego wyciągania danych z Azure
        private bool ContainsAny(string text, params string[] keywords)
            => keywords.Any(k => text.Contains(k));

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