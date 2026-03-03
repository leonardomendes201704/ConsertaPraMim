using ConsertaPraMim.Web.TelegramBridge.Hubs;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Services;
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

builder.Services.AddHttpClient("TelegramBotApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(100);
});

builder.Services.AddSingleton<ITelegramConversationStore, TelegramConversationStore>();
builder.Services.AddSingleton<ITelegramBotApiClient, TelegramBotApiClient>();
builder.Services.AddSingleton<ITelegramAttachmentStorage, TelegramAttachmentStorage>();
builder.Services.AddSingleton<ITelegramChatRealtimeNotifier, TelegramChatRealtimeNotifier>();
builder.Services.AddSingleton<ITelegramChatService, TelegramChatService>();
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
