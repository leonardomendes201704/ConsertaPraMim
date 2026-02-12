using ConsertaPraMim.Infrastructure;
using ConsertaPraMim.Application;
using Microsoft.AspNetCore.Authentication.Cookies;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Clean Architecture Layers
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddHttpClient();
builder.Services.AddScoped<INotificationService, ApiNotificationService>();

// Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ConsertaPraMim.Client.Auth";
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ConsertaPraMim.Infrastructure.Hubs.NotificationHub>("/notificationHub");
app.MapHub<ConsertaPraMim.Infrastructure.Hubs.ChatHub>("/chatHub");

app.Run();
