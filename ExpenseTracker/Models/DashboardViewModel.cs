using System.Collections.Generic;

namespace ExpenseTracker.Models
{
    public class DashboardViewModel
    {
        public string SelectedPeriod { get; set; } = "week";
        public decimal TotalSpent { get; set; }
        public string? TopCategory { get; set; }
        public int TransactionCount { get; set; }

        public List<string> CategoryLabels { get; set; } = new List<string>();
        public List<decimal> CategoryData { get; set; } = new List<decimal>();

        public List<string> WeeklyLabels { get; set; } = new List<string>();
        public List<decimal> WeeklyData { get; set; } = new List<decimal>();
    }
}