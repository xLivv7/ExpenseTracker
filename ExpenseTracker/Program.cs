using ExpenseTracker.Data;        
using ExpenseTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);
var cultureInfo = new CultureInfo("pl-PL");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

// DI
builder.Services.AddControllersWithViews();

// rejestracja bazy
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Rejestracja systemu Identity
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>();

//rejestruje serwis RSS
builder.Services.AddHttpClient<IReceiptScannerService, ReceiptScannerService>();
//singleton -> scoped po EF
builder.Services.AddScoped<ExpenseService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// middleware wbudowany
app.UseHttpsRedirection();//przekierowanie na bezpieczne po��czenie
app.UseStaticFiles();//obrazki,css etc

app.UseRouting();//przekierowywanie

app.UseAuthentication();//bezpieczenstwo
app.UseAuthorization();//jak wyzej

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        //Czeka aż baza się 'obudzi;
        dbContext.Database.SetCommandTimeout(60);
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        //Jeśli będzie błąd to strona wstanie a błąd będzie w logach
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Problem z migracją bazy danych przy starcie.");
    }
}

app.Run();