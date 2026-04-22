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
        public List<Expense> GetAllExpenses(string userId)
        {
            return _context.Expenses.Where(e => e.UserId == userId).ToList();
        }

        // Dodawanie nowego wydatku do bazy
        public void AddExpense(Expense expense, string userId)
        {
            expense.UserId = userId; // Przypisanie ID zalogowanego usera

            if (expense.Date == default)
            {
                expense.Date = DateTime.Now;
            }

            _context.Expenses.Add(expense);
            _context.SaveChanges();
        }

        // Usuwanie z bazy danych
        public bool DeleteExpense(int id, string userId)
        {
            var expense = _context.Expenses.FirstOrDefault(e => e.Id == id && e.UserId == userId);
            if (expense != null)
            {
                _context.Expenses.Remove(expense);
                _context.SaveChanges();
                return true;
            }
            return false;
        }

        // Pobieranie pojedynczego wydatku (potrzebne do załadowania formularza edycji)
        public Expense GetExpenseById(int id, string userId)
        {
            return _context.Expenses.FirstOrDefault(e => e.Id == id && e.UserId == userId);
        }

        // Aktualizacja istniejącego wydatku w bazie
        public bool UpdateExpense(Expense updatedExpense, string userId)
        {
            // Szukamy oryginalnego wydatku w bazie
            var existingExpense = _context.Expenses.FirstOrDefault(e => e.Id == updatedExpense.Id && e.UserId == userId);

            if (existingExpense != null)
            {
                // Aktualizujemy tylko dozwolone pola
                existingExpense.Amount = updatedExpense.Amount;
                existingExpense.Category = updatedExpense.Category;
                existingExpense.SubCategory = updatedExpense.SubCategory;
                existingExpense.Description = updatedExpense.Description;
                existingExpense.Date = updatedExpense.Date;

                _context.SaveChanges();
                return true;
            }

            return false;
        }
    }
}