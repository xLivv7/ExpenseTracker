using ExpenseTracker.Data;
using ExpenseTracker.Models;
using System.Collections.Generic;
using System.Linq;

namespace ExpenseTracker.Services
{
    public class ExpenseService
    {
        // Prywatna zmienna przechowująca dostęp do bazy danych
        private readonly ApplicationDbContext _context;

        // Wsrzykniecie bazy
        public ExpenseService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Pobieranie wszystkich wydatków z bayZ
        public List<Expense> GetAllExpenses()
        {
            return _context.Expenses.ToList();
        }

        // Dodawanie nowego wydatku do bazy
        public void AddExpense(Expense expense)
        {
            // auto ID
            if (expense.Date == default)
            {
                expense.Date = DateTime.Now;
            }

            _context.Expenses.Add(expense); // Dodanie do ;kolejki'
            _context.SaveChanges();         // Zapis w bazie
        }

        // Usuwanie z bazy danych
        public bool DeleteExpense(int id)
        {
            var expense = _context.Expenses.FirstOrDefault(e => e.Id == id);
            if (expense != null)
            {
                _context.Expenses.Remove(expense);
                _context.SaveChanges();     
                return true;
            }
            return false;
        }
    }
}