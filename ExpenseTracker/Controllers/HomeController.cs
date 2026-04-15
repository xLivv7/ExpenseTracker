using ExpenseTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using ExpenseTracker.Data; // Upewnij siê, ¿e to pasuje do Twojego projektu
using System.Security.Claims; // Potrzebne do filtrowania po zalogowanym u¿ytkowniku

namespace ExpenseTracker.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        // Konstruktor z wstrzykniêtym kontekstem bazy
        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public IActionResult Dashboard(string period = "week") // Domyœlnie ³aduje "week"
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var allExpenses = _context.Expenses.Where(e => e.UserId == currentUserId).ToList();

            // Zabezpieczenie przed pust¹ baz¹
            if (allExpenses == null || !allExpenses.Any())
            {
                return View(new DashboardViewModel { SelectedPeriod = period });
            }

            DateTime startDate;
            DateTime today = DateTime.Today;

            // 1. Ustalenie daty pocz¹tkowej na podstawie wybranego okresu
            switch (period.ToLower())
            {
                case "month":
                    startDate = new DateTime(today.Year, today.Month, 1); // Pierwszy dzieñ obecnego miesi¹ca
                    break;
                case "year":
                    startDate = new DateTime(today.Year, 1, 1); // Pierwszy dzieñ obecnego roku
                    break;
                case "week":
                default:
                    startDate = today.AddDays(-6); // Ostatnie 7 dni
                    period = "week";
                    break;
            }

            // 2. Filtrujemy wydatki TYLKO dla wybranego zakresu czasu
            var expenses = allExpenses.Where(e => e.Date.Date >= startDate.Date && e.Date.Date <= today).ToList();

            // 3. Statystyki ogólne (liczone tylko z przefiltrowanych wydatków)
            var viewModel = new DashboardViewModel
            {
                SelectedPeriod = period, // Zapisujemy, co wybra³ u¿ytkownik
                TotalSpent = expenses.Sum(e => e.Amount),
                TransactionCount = expenses.Count,
                TopCategory = expenses.GroupBy(e => e.Category)
                                      .OrderByDescending(g => g.Sum(e => e.Amount))
                                      .Select(g => g.Key)
                                      .FirstOrDefault() ?? "Brak"
            };

            // 4. Dane do wykresu ko³owego
            var categoryStats = expenses.GroupBy(e => e.Category)
                                        .Select(g => new {
                                            Name = g.Key,
                                            SumAmount = g.Sum(e => e.Amount)
                                        }).ToList();

            viewModel.CategoryLabels = categoryStats.Select(x => x.Name).ToList();
            viewModel.CategoryData = categoryStats.Select(x => x.SumAmount).ToList();

            // 5. Dane do wykresu s³upkowego - Zale¿ne od okresu
            if (period == "week")
            {
                for (int i = 6; i >= 0; i--)
                {
                    var date = today.AddDays(-i);
                    viewModel.WeeklyLabels.Add(date.ToString("dd.MM"));
                    viewModel.WeeklyData.Add(expenses.Where(e => e.Date.Date == date.Date).Sum(e => e.Amount));
                }
            }
            else if (period == "month")
            {
                int daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
                for (int i = 1; i <= daysInMonth; i++)
                {
                    viewModel.WeeklyLabels.Add($"{i:D2}"); // Dodaje dni jako 01, 02... 30
                    viewModel.WeeklyData.Add(expenses.Where(e => e.Date.Day == i).Sum(e => e.Amount));
                }
            }
            else if (period == "year")
            {
                string[] monthNames = { "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "PaŸ", "Lis", "Gru" };
                for (int i = 1; i <= 12; i++)
                {
                    viewModel.WeeklyLabels.Add(monthNames[i - 1]);
                    viewModel.WeeklyData.Add(expenses.Where(e => e.Date.Month == i).Sum(e => e.Amount));
                }
            }

            return View(viewModel);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}