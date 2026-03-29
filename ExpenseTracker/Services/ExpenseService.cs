using ExpenseTracker.Models;

namespace ExpenseTracker.Services
{
    public class ExpenseService
    {
        private readonly List<Expense> _expenses = new List<Expense>();

        public List<Expense> GetAllExpenses()
        {
            return _expenses;
        }

        public void AddExpense(Expense expense)
        {
            expense.Id = _expenses.Count > 0 ? _expenses.Max(e => e.Id) + 1 : 1;

            if (expense.Date == default)
            {
                expense.Date = DateTime.Now;
            }

            _expenses.Add(expense);
        }

        public bool DeleteExpense(int id)
        {
            var expense = _expenses.FirstOrDefault(e => e.Id == id);
            if (expense != null)
            {
                _expenses.Remove(expense);
                return true;
            }
            return false;
        }
    }
}