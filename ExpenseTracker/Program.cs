using ExpenseTracker.Data;        
using ExpenseTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// DI
builder.Services.AddControllersWithViews();

// rejestracja bazy
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Rejestracja systemu Identity
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>();

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

app.Run();