namespace ExpenseTracker.Models
{
    public class VerifyReceiptViewModel
    {
        public string MerchantName { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public DateTime Date { get; set; }

        // Zamiast jednego wydatku, przekażemy do widoku całą listę gotową do edycji/zapisu
        public List<Expense> ExpensesToSave { get; set; } = new List<Expense>();
    }
}