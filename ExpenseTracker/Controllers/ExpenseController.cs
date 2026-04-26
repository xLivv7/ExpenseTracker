using ExpenseTracker.Models;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    public class ExpenseController : Controller
    {
        private readonly ExpenseService _expenseService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IReceiptScannerService _receiptScannerService; // NOWE: Serwis AI

        // Zaktualizowany konstruktor wstrzykujący IReceiptScannerService
        public ExpenseController(ExpenseService expenseService, UserManager<IdentityUser> userManager, IReceiptScannerService receiptScannerService)
        {
            _expenseService = expenseService;
            _userManager = userManager;
            _receiptScannerService = receiptScannerService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var userId = _userManager.GetUserId(User);
            var data = _expenseService.GetAllExpenses(userId);
            return View(data);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(Expense newExpense)
        {
            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);
                _expenseService.AddExpense(newExpense, userId);
                return RedirectToAction("Index");
            }
            return View(newExpense);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            bool deleted = _expenseService.DeleteExpense(id, userId);

            if (deleted)
            {
                return RedirectToAction(nameof(Index));
            }

            return NotFound();
        }

        [HttpGet]
        public IActionResult Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            var expense = _expenseService.GetExpenseById(id.Value, currentUserId);

            if (expense == null)
            {
                return NotFound();
            }

            return View(expense);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, [Bind("Id,Amount,Category,Subcategory,Description,Date")] Expense expense)
        {
            if (id != expense.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var currentUserId = _userManager.GetUserId(User);
                bool isUpdated = _expenseService.UpdateExpense(expense, currentUserId);

                if (isUpdated)
                {
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    return NotFound();
                }
            }

            return View(expense);
        }

        // --- NOWE AKCJE DO OBSŁUGI SKANOWANIA AI ---

        [HttpGet]
        public IActionResult Scan()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadReceipt(IFormFile receiptFile)
        {
            // 1. Walidacja, czy przesłano plik
            if (receiptFile == null || receiptFile.Length == 0)
            {
                ModelState.AddModelError("", "Proszę wybrać plik obrazu.");
                return View("Scan");
            }

            // 2. Wysłanie do AI
            using var stream = receiptFile.OpenReadStream();
            var scannedDto = await _receiptScannerService.ScanReceiptAsync(stream);

            // 3. Obsługa przypadku, gdy AI nie odczyta danych
            if (scannedDto == null)
            {
                TempData["ErrorMessage"] = "Nie udało się odczytać paragonu. Wpisz dane ręcznie.";
                return RedirectToAction("Create");
            }

            // 4. Mapowanie danych z DTO na Twój model Expense
            var expense = new Expense
            {
                Amount = scannedDto.TotalAmount ?? 0,
                Date = scannedDto.TransactionDate ?? DateTime.Today,
                Category = scannedDto.Category ?? "", // Jeśli puste, pole w widoku będzie puste i walidacja wymusi na użytkowniku wybór
                SubCategory = scannedDto.SubCategory,
                Description = scannedDto.Description ?? "Skan paragonu"
            };

            TempData["SuccessMessage"] = "Paragon odczytany pomyślnie! Zweryfikuj i zapisz dane.";

            // Zwracamy widok "Create", podając mu wypełniony obiekt Expense
            return View("Create", expense);
        }
    }
}