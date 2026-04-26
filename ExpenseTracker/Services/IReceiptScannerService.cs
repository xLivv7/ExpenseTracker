using ExpenseTracker.Models;

namespace ExpenseTracker.Services
{
    public interface IReceiptScannerService
    {
        Task<ScannedReceiptDto?> ScanReceiptAsync(Stream imageStream);
    }
}