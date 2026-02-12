using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services;
using DoctorEngineApp.Services.Implementations;
using DoctorEngineApp.Services.Interfaces;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;

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
builder.Services.AddScoped<ISubServiceService, SubServiceService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddScoped<ITechnicianService, TechnicianService>();
builder.Services.AddScoped<IContactInfoService, ContactInfoService>();
builder.Services.AddScoped<IContactPhoneService,ContactPhoneService>();
builder.Services.AddScoped<IMapService, MapService>();
builder.Services.AddScoped<IContactMessageService, ContactMessageService>();
builder.Services.AddScoped<IWarrantyService, WarrantyService>();






builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 2_000_000_000; // e.g. 2GB
});

// If using IIS, adjust configuration accordingly (web.config)
// For forms, if needed:
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 2_000_000_000; // 2GB
});


// HttpContext
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();


RotativaConfiguration.Setup(
    Directory.GetCurrentDirectory(),
    "wwwroot/Rotativa"
);
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
