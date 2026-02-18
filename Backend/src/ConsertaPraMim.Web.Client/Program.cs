using Microsoft.AspNetCore.Authentication.Cookies;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Web.Client.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ClientApiCaller>();
builder.Services.AddScoped<IClientAuthApiClient, ClientAuthApiClient>();
builder.Services.AddScoped<IClientDashboardApiClient, ClientDashboardApiClient>();
builder.Services.AddScoped<IClientProposalApiClient, ClientProposalApiClient>();
builder.Services.AddScoped<IClientChatApiClient, ClientChatApiClient>();
builder.Services.AddScoped<IServiceRequestService, ClientApiServiceRequestService>();
builder.Services.AddScoped<IServiceCategoryCatalogService, ClientApiServiceCategoryCatalogService>();
builder.Services.AddScoped<IProposalService, ClientApiProposalService>();
builder.Services.AddScoped<IProviderGalleryService, ClientApiProviderGalleryService>();
builder.Services.AddScoped<IZipGeocodingService, ClientApiZipGeocodingService>();
builder.Services.AddScoped<IServiceAppointmentService, ClientApiServiceAppointmentService>();
builder.Services.AddScoped<IServiceAppointmentChecklistService, ClientApiServiceAppointmentChecklistService>();
builder.Services.AddScoped<IReviewService, ClientApiReviewService>();
builder.Services.AddScoped<IProfileService, ClientApiProfileService>();
builder.Services.AddScoped<IPaymentReceiptService, ClientApiPaymentReceiptService>();
builder.Services.AddScoped<IPaymentCheckoutService, ClientApiPaymentCheckoutService>();
builder.Services.AddScoped<IPaymentWebhookService, ClientApiPaymentWebhookService>();
var apiOrigin = ResolveOrigin(builder.Configuration["ApiBaseUrl"]);

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
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] = BuildContentSecurityPolicy(apiOrigin, app.Environment.IsDevelopment());

    await next();
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static string? ResolveOrigin(string? url)
{
    if (string.IsNullOrWhiteSpace(url))
    {
        return null;
    }

    return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        ? uri.GetLeftPart(UriPartial.Authority)
        : null;
}

static string BuildContentSecurityPolicy(string? apiOrigin, bool isDevelopment)
{
    var connectSources = new List<string> { "'self'" };
    var mediaSources = new List<string> { "'self'", "data:", "blob:", "https://ui-avatars.com" };
    var imageSources = new List<string> { "'self'", "data:", "blob:", "https://ui-avatars.com" };

    if (!string.IsNullOrWhiteSpace(apiOrigin))
    {
        connectSources.Add(apiOrigin);
        imageSources.Add(apiOrigin);
        mediaSources.Add(apiOrigin);

        if (Uri.TryCreate(apiOrigin, UriKind.Absolute, out var originUri))
        {
            var wsScheme = originUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
            connectSources.Add($"{wsScheme}://{originUri.Authority}");
        }
    }

    if (isDevelopment)
    {
        // Permite acesso via LAN e ferramentas de desenvolvimento (BrowserLink/Hot Reload).
        // Em CSP, use scheme-source (http:, ws:, etc) em vez de host curinga "http://*".
        connectSources.AddRange(new[] { "http:", "https:", "ws:", "wss:" });
        imageSources.AddRange(new[] { "http:", "https:" });
        mediaSources.AddRange(new[] { "http:", "https:" });
    }

    return string.Join(
        " ",
        new[]
        {
            "default-src 'self';",
            "base-uri 'self';",
            "frame-ancestors 'none';",
            "object-src 'none';",
            "form-action 'self';",
            $"connect-src {string.Join(' ', connectSources.Distinct(StringComparer.OrdinalIgnoreCase))};",
            $"img-src {string.Join(' ', imageSources.Distinct(StringComparer.OrdinalIgnoreCase))};",
            $"media-src {string.Join(' ', mediaSources.Distinct(StringComparer.OrdinalIgnoreCase))};",
            "font-src 'self' https://cdnjs.cloudflare.com https://fonts.gstatic.com;",
            "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://fonts.googleapis.com;",
            "script-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com;"
        });
}
