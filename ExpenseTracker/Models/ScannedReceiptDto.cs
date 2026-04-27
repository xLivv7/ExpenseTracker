namespace ExpenseTracker.Models
{
    public class ScannedReceiptDto
    {
        public string? MerchantName { get; set; }
        public decimal? TotalAmount { get; set; }
        public DateTime? TransactionDate { get; set; }
        public string? Category { get; set; } //Główna kategoria

        //Lista-każda wykryta podkategoria
        public List<SubCategorySummaryDto> SubCategories { get; set; } = new();
    }

    public class SubCategorySummaryDto
    {
        public string Category { get; set; }
        public string SubCategory { get; set; }
        public decimal Amount { get; set; }
        public List<string> ItemNames { get; set; } = new();
    }
    //Tylko do odczytania odp z AI
    public class OpenAiItemResponse
    {
        public string OriginalName { get; set; }
        public string Category { get; set; }
        public string SubCategory { get; set; }
    }
}