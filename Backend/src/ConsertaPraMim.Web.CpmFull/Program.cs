using System.Text.Json;
using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.Integrations.Telegram;
using AppMobileCPM.Observability;
using AppMobileCPM.Services;
using System.IO.Compression;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddSingleton<IMarketplaceRepository, SqlMarketplaceRepository>();
builder.Services.AddSingleton<IAdminAuthService, SqlAdminAuthService>();
builder.Services.AddSingleton<IAdminSiteContentService, SqlAdminSiteContentService>();
builder.Services.AddSingleton<IAdminSupportFaqService, SqlAdminSupportFaqService>();
builder.Services.AddSingleton<IAdminKanbanService, SqlAdminKanbanService>();
builder.Services.AddScoped<ISiteContentResolver, SiteContentResolver>();
builder.Services.AddScoped<IChatwootSyncQueueService, ChatwootSyncQueueService>();
builder.Services.AddScoped<IChatwootLeadSyncService, ChatwootLeadSyncService>();
builder.Services.AddScoped<IChatwootBackfillService, ChatwootBackfillService>();
builder.Services.AddScoped<IChatwootWebhookService, ChatwootWebhookService>();
builder.Services.AddScoped<IJourneyAutomationService, JourneyAutomationService>();
builder.Services.AddScoped<IJourneyQualificationService, JourneyQualificationService>();
builder.Services.AddScoped<IJourneySchedulingService, JourneySchedulingService>();
builder.Services.AddScoped<IJourneyProviderMatchingService, JourneyProviderMatchingService>();
builder.Services.AddScoped<IJourneyProviderDispatchService, JourneyProviderDispatchService>();
builder.Services.AddScoped<IJourneyProviderDispatchNotificationService, JourneyProviderDispatchNotificationService>();
builder.Services.AddScoped<IJourneyProviderConnectionService, JourneyProviderConnectionService>();
builder.Services.AddScoped<IJourneyProviderOpportunityService, JourneyProviderOpportunityService>();
builder.Services.AddScoped<IJourneyStageAutomationService, JourneyStageAutomationService>();
builder.Services.AddScoped<ITelegramLeadAutomationService, TelegramLeadAutomationService>();
builder.Services.AddScoped<ITelegramDeliveryQueueService, TelegramDeliveryQueueService>();
builder.Services.AddScoped<ITelegramMessageAutomationService, TelegramMessageAutomationService>();
builder.Services.AddSingleton<IJourneyCalendarGateway, JourneyGoogleCalendarGateway>();
builder.Services.AddSingleton<IJourneyGeocodingService, JourneyGeocodingService>();
builder.Services.AddSingleton<IJourneyQualificationAiGateway, JourneyQualificationAiGateway>();
builder.Services.AddSingleton<IJourneyProviderDispatchLinkService, JourneyProviderDispatchLinkService>();
builder.Services.AddHostedService<ChatwootSyncRetryWorker>();
builder.Services.AddHostedService<ChatwootWebhookRetentionWorker>();
builder.Services.AddHostedService<JourneyProviderMatchingWorker>();
builder.Services.AddHostedService<JourneyProviderDispatchWorker>();
builder.Services.AddHostedService<JourneyStageAutomationWorker>();
builder.Services.AddHostedService<TelegramDeliveryWorker>();
builder.Services.AddHostedService<TelegramDeliveryRetentionWorker>();
builder.Services.AddSingleton<IValidateOptions<ChatwootOptions>, ChatwootOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<JourneyAutomationOptions>, JourneyAutomationOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<JourneyQualificationOptions>, JourneyQualificationOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<JourneySchedulingOptions>, JourneySchedulingOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<JourneyProviderMatchingOptions>, JourneyProviderMatchingOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<JourneyProviderDispatchOptions>, JourneyProviderDispatchOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<JourneyProviderNotificationOptions>, JourneyProviderNotificationOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<JourneyStageAutomationOptions>, JourneyStageAutomationOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<TelegramAutomationOptions>, TelegramAutomationOptionsValidator>();
builder.Services.AddOptions<ChatwootOptions>()
    .Bind(builder.Configuration.GetSection(ChatwootOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<JourneyAutomationOptions>()
    .Bind(builder.Configuration.GetSection(JourneyAutomationOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<JourneyQualificationOptions>()
    .Bind(builder.Configuration.GetSection(JourneyQualificationOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<JourneySchedulingOptions>()
    .Bind(builder.Configuration.GetSection(JourneySchedulingOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<JourneyProviderMatchingOptions>()
    .Bind(builder.Configuration.GetSection(JourneyProviderMatchingOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<JourneyProviderDispatchOptions>()
    .Bind(builder.Configuration.GetSection(JourneyProviderDispatchOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<JourneyProviderNotificationOptions>()
    .Bind(builder.Configuration.GetSection(JourneyProviderNotificationOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<JourneyStageAutomationOptions>()
    .Bind(builder.Configuration.GetSection(JourneyStageAutomationOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<TelegramAutomationOptions>()
    .Bind(builder.Configuration.GetSection(TelegramAutomationOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddHttpClient<IChatwootApiClient, ChatwootApiClient>((serviceProvider, client) =>
{
    var chatwootOptions = serviceProvider.GetRequiredService<IOptions<ChatwootOptions>>().Value;
    if (Uri.TryCreate(chatwootOptions.BaseUrl, UriKind.Absolute, out var baseUri))
    {
        client.BaseAddress = baseUri;
    }

    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "ConsertaPraMim.Web.CpmFull/1.0");
});
builder.Services.AddHttpClient<ITelegramBridgeDeliveryClient, TelegramBridgeDeliveryClient>((serviceProvider, client) =>
{
    var telegramOptions = serviceProvider.GetRequiredService<IOptions<TelegramAutomationOptions>>().Value;
    if (Uri.TryCreate(telegramOptions.TelegramBridgeBaseUrl, UriKind.Absolute, out var baseUri))
    {
        client.BaseAddress = baseUri;
    }

    client.Timeout = telegramOptions.GetRequestTimeout();
    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "ConsertaPraMim.Web.CpmFull/1.0");
});
builder.Services.AddHttpClient<ITelegramBridgeObservabilityClient, TelegramBridgeObservabilityClient>((serviceProvider, client) =>
{
    var telegramOptions = serviceProvider.GetRequiredService<IOptions<TelegramAutomationOptions>>().Value;
    if (Uri.TryCreate(telegramOptions.TelegramBridgeBaseUrl, UriKind.Absolute, out var baseUri))
    {
        client.BaseAddress = baseUri;
    }

    client.Timeout = telegramOptions.GetRequestTimeout();
    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "ConsertaPraMim.Web.CpmFull/1.0");
});
builder.Services.AddHealthChecks()
    .AddCheck<ChatwootConnectionHealthCheck>("chatwoot_connection");
builder.Services.AddAuthentication(AdminAuthConstants.AuthenticationScheme)
    .AddCookie(AdminAuthConstants.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "cpm_admin_auth";
        options.LoginPath = "/admin/login";
        options.AccessDeniedPath = "/admin/login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
    });
builder.Services.AddAuthorization();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/xml", "text/xml", "image/svg+xml"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        const int maxAgeInSeconds = 60 * 60 * 24 * 30;
        context.Context.Response.Headers.CacheControl = $"public,max-age={maxAgeInSeconds}";
    }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Text("Healthy", "text/plain"));
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHealthChecks("/internal/health/chatwoot", new HealthCheckOptions
{
    Predicate = registration => registration.Name == "chatwoot_connection",
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    error = entry.Value.Exception?.Message
                })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
});

app.Run();
