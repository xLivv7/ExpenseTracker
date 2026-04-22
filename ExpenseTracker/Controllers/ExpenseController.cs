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

        // Wstrzykujemy UserManager
        public ExpenseController(ExpenseService expenseService, UserManager<IdentityUser> userManager)
        {
            _expenseService = expenseService;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Pobieramy ID aktualnie zalogowanego użytkownika
            var userId = _userManager.GetUserId(User);

            // Przekazujemy to ID do serwisu
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
                _expenseService.AddExpense(newExpense, userId); // Przekazujemy ID!
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

    }
}