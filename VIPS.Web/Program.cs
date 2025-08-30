using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using VIPS.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using System.Globalization;


var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddControllersWithViews();
//builder.Services.AddEndpointsApiExplorer();




builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "VIPS.Cookie";
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
    });



builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<IHashService, HashService>();
builder.Services.AddScoped<LogService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ClientService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<FleetService>();




builder.Services.AddSingleton(new EmailService(
    smtpHost: "smtp.gmail.com",
    smtpPort: 587,
    smtpUser: "lautarogomizelj@gmail.com",
    smtpPass: "ddgwvzdgdkcckjmn",
    fromEmail: "lautarogomizelj@gmail.com",
    fromName: "Soporte VIPS"
));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error"); //cambiar
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication(); //  Debe ir ANTES de UseAuthorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "adminGeneral",
    pattern: "AdminGeneral/{action=Index}/{id?}",
    defaults: new { controller = "AdminGeneral" });

app.MapControllerRoute(
    name: "adminLogistico",
    pattern: "AdminLogistico/{action=Index}/{id?}",
    defaults: new { controller = "AdminLogistico" });


app.MapControllerRoute(
    name: "adminVentas",
    pattern: "AdminVentas/{action=Index}/{id?}",
    defaults: new { controller = "AdminVentas" });



app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");



app.Run();
