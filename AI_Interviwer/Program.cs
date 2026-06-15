using AI_Interviwer.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Add MVC Controllers With Views
builder.Services.AddControllersWithViews();

// 2. Database Connection (Bina ServiceProvider build kiye, direct clean tareeqa)
builder.Services.AddDbContext<AIDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("dbcs")));

// 3. Session Configuration (Jo error aa raha tha usay fix karne ke liye)
// 3. Session Configuration (Ab bilkul sahi property ke sath)
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromMinutes(30); // User 30 mins tak logged in rahega
    options.Cookie.HttpOnly = true;                 // Cookie.HttpOnly use hoga
    options.Cookie.IsEssential = true;              // Cookie.IsEssential use hoga
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 4. Session Middleware (Routing ke BAAD aur Authorization se PEHLE hona lazmi hai!)
app.UseSession();

app.UseAuthorization();

// 5. Default Route Configuration
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"); // Pehle login page khulega

app.Run();