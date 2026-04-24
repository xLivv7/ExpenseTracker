namespace ExpenseTracker.Models
{
    public class ScannedReceiptDto
    {
        public string? MerchantName { get; set; }
        public decimal? TotalAmount { get; set; }
        public DateTime? TransactionDate { get; set; }
        public string? Category { get; set; }
        public string? SubCategory { get; set; }
        public string? Description { get; set; }
    }
}
