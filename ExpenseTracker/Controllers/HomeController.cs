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
        public IActionResult Dashboard()
        {
            //pobieranie wydatjow tylko dla danego usera
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var expenses = _context.Expenses.Where(e => e.UserId == userId).ToList();

            // Obs³uga braku danych
            if (expenses == null || !expenses.Any())
            {
                return View(new DashboardViewModel());
            }

            //Ogólne statystyki
            var viewModel = new DashboardViewModel
            {
                TotalSpent = expenses.Sum(e => e.Amount),
                TransactionCount = expenses.Count,
                TopCategory = expenses.GroupBy(e => e.Category) 
                                      .OrderByDescending(g => g.Sum(e => e.Amount)) 
                                      .Select(g => g.Key)
                                      .FirstOrDefault() ?? "Brak"
            };

            //Dane do wykresu ko³owego (GroupBy category)
            var categoryStats = expenses.GroupBy(e => e.Category) 
                                        .Select(g => new {
                                            Name = g.Key,
                                            SumAmount = g.Sum(e => e.Amount) 
                                        }).ToList();

            viewModel.CategoryLabels = categoryStats.Select(x => x.Name).ToList();
            viewModel.CategoryData = categoryStats.Select(x => x.SumAmount).ToList();

            //Dane do wykresu s³upkowego (Ostatnie 7 dni)
            var today = DateTime.Today;
            for (int i = 6; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                viewModel.WeeklyLabels.Add(date.ToString("dd.MM"));

                //Sumujemy wydatki z konkretnego dnia
                var daySum = expenses.Where(e => e.Date.Date == date.Date).Sum(e => e.Amount); 
                viewModel.WeeklyData.Add(daySum);
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