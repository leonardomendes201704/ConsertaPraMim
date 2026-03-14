using ConsertaPraMim.Web.TelegramBridge.Hubs;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IValidateOptions<TelegramBridgeOptions>, TelegramBridgeOptionsValidator>();
builder.Services.AddOptions<TelegramBridgeOptions>()
    .Bind(builder.Configuration.GetSection(TelegramBridgeOptions.SectionName))
    .ValidateOnStart();
builder.Services.Configure<TelegramBridgeAiOptions>(
    builder.Configuration.GetSection(TelegramBridgeAiOptions.SectionName));
builder.Services.Configure<TelegramChatbotRolloutOptions>(
    builder.Configuration.GetSection(TelegramChatbotRolloutOptions.SectionName));
builder.Services.Configure<TelegramChatbotObservabilityOptions>(
    builder.Configuration.GetSection(TelegramChatbotObservabilityOptions.SectionName));
builder.Services.AddSingleton<IValidateOptions<TelegramAutomationOptions>, TelegramAutomationOptionsValidator>();
builder.Services.AddOptions<TelegramAutomationOptions>()
    .Bind(builder.Configuration.GetSection(TelegramAutomationOptions.SectionName))
    .ValidateOnStart();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52_428_800;
});

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();
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
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                if (IsApiOrHubRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                if (IsApiOrHubRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddHttpClient("TelegramBotApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(100);
});
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<ITelegramLeadAutomationClient, TelegramLeadAutomationClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TelegramAutomationOptions>>().Value;
    if (Uri.TryCreate(options.CpmFullBaseUrl, UriKind.Absolute, out var baseUri))
    {
        client.BaseAddress = baseUri;
    }

    client.Timeout = options.GetRequestTimeout();
});
builder.Services.AddHttpClient<ITelegramMessageAutomationClient, TelegramMessageAutomationClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TelegramAutomationOptions>>().Value;
    if (Uri.TryCreate(options.CpmFullBaseUrl, UriKind.Absolute, out var baseUri))
    {
        client.BaseAddress = baseUri;
    }

    client.Timeout = options.GetRequestTimeout();
});

builder.Services.AddSingleton<ITelegramConversationStore, TelegramConversationStore>();
builder.Services.AddSingleton<ITelegramBotApiClient, TelegramBotApiClient>();
builder.Services.AddSingleton<ITelegramAttachmentStorage, TelegramAttachmentStorage>();
builder.Services.AddSingleton<ITelegramChatRealtimeNotifier, TelegramChatRealtimeNotifier>();
builder.Services.AddSingleton<ITelegramChatService, TelegramChatService>();
builder.Services.AddSingleton<ITelegramInboundUpdateProcessor, TelegramInboundUpdateProcessor>();
builder.Services.AddSingleton<ITelegramHumanHandoffStateService, TelegramHumanHandoffStateService>();
builder.Services.AddScoped<ITelegramBridgeAuthApiClient, TelegramBridgeAuthApiClient>();
builder.Services.AddScoped<ITelegramChatbotApiClient, TelegramChatbotApiClient>();
builder.Services.AddSingleton<ITelegramAiGateway, OpenAiTelegramGateway>();
builder.Services.AddSingleton<ITelegramChatbotFeatureFlagService, TelegramChatbotFeatureFlagService>();
builder.Services.AddSingleton<ITelegramChatbotObservabilityService, TelegramChatbotObservabilityService>();
builder.Services.AddSingleton<TelegramServiceRequestTriageEngine>();
builder.Services.AddSingleton<TelegramSchedulingNaturalLanguageParser>();
builder.Services.AddScoped<ITelegramChatbotOrchestrator, TelegramChatbotOrchestrator>();
builder.Services.AddHostedService<TelegramUpdateTransportBootstrapService>();
builder.Services.AddHostedService<TelegramLongPollingBackgroundService>();
builder.Services.AddHostedService<TelegramAttachmentRetentionWorker>();

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

static bool IsApiOrHubRequest(HttpRequest request)
{
    return request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        || request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase);
}
