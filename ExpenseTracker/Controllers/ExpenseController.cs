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
            if (receiptFile == null || receiptFile.Length == 0)
            {
                ModelState.AddModelError("", "Proszę wybrać plik obrazu.");
                return View("Scan");
            }

            using var stream = receiptFile.OpenReadStream();
            var scannedDto = await _receiptScannerService.ScanReceiptAsync(stream);

            if (scannedDto == null)
            {
                TempData["ErrorMessage"] = "Nie udało się odczytać paragonu. Wpisz dane ręcznie.";
                return RedirectToAction("Create");
            }

            // TWORZYMY NASZ NOWY WIDOK Z WERYFIKACJĄ WIELU WYDATKÓW
            var viewModel = new VerifyReceiptViewModel
            {
                MerchantName = scannedDto.MerchantName ?? "Nieznany sklep",
                TotalAmount = scannedDto.TotalAmount ?? 0,
                Date = scannedDto.TransactionDate ?? DateTime.Today,
                ExpensesToSave = new List<Expense>()
            };

            // Przerabiamy pogrupowane podkategorie z AI na listę wydatków
            if (scannedDto.SubCategories != null && scannedDto.SubCategories.Any())
            {
                foreach (var subCategory in scannedDto.SubCategories)
                {
                    viewModel.ExpensesToSave.Add(new Expense
                    {
                        Amount = subCategory.Amount,
                        Date = scannedDto.TransactionDate ?? DateTime.Today,
                        Category = subCategory.Category,
                        SubCategory = subCategory.SubCategory,
                        // AI wrzuca w itemNames np. ["Mleko", "Ser"], łączymy to po przecinku:
                        Description = string.Join(", ", subCategory.ItemNames)
                    });
                }
            }
            else
            {
                // Fallback: Jeśli AI nie znajdzie listy produktów, dodajemy po prostu cały paragon jako jeden wydatek
                viewModel.ExpensesToSave.Add(new Expense
                {
                    Amount = scannedDto.TotalAmount ?? 0,
                    Date = scannedDto.TransactionDate ?? DateTime.Today,
                    Category = scannedDto.Category ?? "Inne",
                    Description = $"Zakupy: {scannedDto.MerchantName}"
                });
            }

            TempData["SuccessMessage"] = "Paragon odczytany pomyślnie! Zweryfikuj kategorie i zapisz.";
            return View("VerifyReceipt", viewModel); // Odsyłamy do nowego widoku!
        }

        // NOWA AKCJA ZAPISUJĄCA WIELE WYDATKÓW NA RAZ
        [HttpPost]
        public IActionResult SaveVerifiedReceipt(VerifyReceiptViewModel model)
        {
            var userId = _userManager.GetUserId(User);

            if (model.ExpensesToSave != null && model.ExpensesToSave.Any())
            {
                foreach (var expense in model.ExpensesToSave)
                {
                    // Opcjonalne zabezpieczenie, żeby nie zapisywać wydatków z kwotą 0
                    if (expense.Amount > 0)
                    {
                        _expenseService.AddExpense(expense, userId);
                    }
                }
            }

            TempData["SuccessMessage"] = "Podzielone wydatki zostały zapisane!";
            return RedirectToAction("Index");
        }
    }
}