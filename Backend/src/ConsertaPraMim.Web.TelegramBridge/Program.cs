using ConsertaPraMim.Web.TelegramBridge.Hubs;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TelegramBridgeOptions>(
    builder.Configuration.GetSection(TelegramBridgeOptions.SectionName));

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52_428_800;
});

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ConsertaPraMim.TelegramBridge.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

builder.Services.AddHttpClient("TelegramBotApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(100);
});
builder.Services.AddHttpClient();

builder.Services.AddSingleton<ITelegramConversationStore, TelegramConversationStore>();
builder.Services.AddSingleton<ITelegramBotApiClient, TelegramBotApiClient>();
builder.Services.AddSingleton<ITelegramAttachmentStorage, TelegramAttachmentStorage>();
builder.Services.AddSingleton<ITelegramChatRealtimeNotifier, TelegramChatRealtimeNotifier>();
builder.Services.AddSingleton<ITelegramChatService, TelegramChatService>();
builder.Services.AddScoped<ITelegramBridgeAuthApiClient, TelegramBridgeAuthApiClient>();
builder.Services.AddScoped<ITelegramChatbotApiClient, TelegramChatbotApiClient>();
builder.Services.AddHostedService<TelegramLongPollingBackgroundService>();

var app = builder.Build();

EnsureUploadDirectory(app.Environment);

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

app.MapHub<TelegramChatHub>("/hubs/telegram-chat");

app.Run();

static void EnsureUploadDirectory(IWebHostEnvironment environment)
{
    var webRoot = environment.WebRootPath;
    if (string.IsNullOrWhiteSpace(webRoot))
    {
        webRoot = Path.Combine(environment.ContentRootPath, "wwwroot");
    }

    var uploadsDirectory = Path.Combine(webRoot, "uploads", "telegram-bridge");
    Directory.CreateDirectory(uploadsDirectory);
}
