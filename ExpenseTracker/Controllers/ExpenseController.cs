using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Controllers
{
    public class ExpenseController : Controller
    {
        private readonly ExpenseService _expenseService;

        public ExpenseController(ExpenseService expenseService)
        {
            _expenseService = expenseService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var data = _expenseService.GetAllExpenses();
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
                _expenseService.AddExpense(newExpense);
                return RedirectToAction("Index");
            }
            return View(newExpense);
        }

        [HttpDelete("Expense/Delete/{id}")]
        public IActionResult Delete(int id)
        {
            bool deleted = _expenseService.DeleteExpense(id);

            if (deleted)
            {
                return Ok();
            }
            return NotFound();
        }
    }
}