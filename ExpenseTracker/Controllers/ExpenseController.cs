using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

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

        [HttpDelete("Expense/Delete/{id}")]
        public IActionResult Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            bool deleted = _expenseService.DeleteExpense(id, userId);

            if (deleted) return Ok();
            return NotFound();
        }
    }
}