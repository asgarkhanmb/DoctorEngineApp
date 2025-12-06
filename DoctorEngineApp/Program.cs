using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services;
using DoctorEngineApp.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Connection string al
var connectionString = builder.Configuration.GetConnectionString("Default");

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// Identity
builder.Services.AddIdentity<AppUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Eğer Identity ayarlarını özelleştirmek istersen açabilirsin
//builder.Services.Configure<IdentityOptions>(opt =>
//{
//    opt.Password.RequiredUniqueChars = 1;
//    opt.Password.RequireNonAlphanumeric = true;
//    opt.Password.RequireDigit = true;
//    opt.Password.RequireLowercase = true;
//    opt.Password.RequireUppercase = true;
//    opt.User.RequireUniqueEmail = true;
//});

// Servisler
builder.Services.AddScoped<ISliderService, SliderService>();
builder.Services.AddScoped<IInformService, InformService>();
builder.Services.AddScoped<IAboutService, AboutService>();


// HttpContext
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

var app = builder.Build();

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Area routing
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Default routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
