using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);
var ptBrCulture = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = ptBrCulture;
CultureInfo.DefaultThreadCurrentUICulture = ptBrCulture;

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAdminAuthApiClient, AdminAuthApiClient>();
builder.Services.AddScoped<IAdminDashboardApiClient, AdminDashboardApiClient>();
builder.Services.AddScoped<IAdminUsersApiClient, AdminUsersApiClient>();
builder.Services.AddScoped<IAdminOperationsApiClient, AdminOperationsApiClient>();
builder.Services.AddScoped<IAdminPortalLinksService, AdminPortalLinksService>();
var apiOrigin = ResolveOrigin(builder.Configuration["ApiBaseUrl"]);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ConsertaPraMim.Admin.Auth";
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(ptBrCulture),
    SupportedCultures = new List<CultureInfo> { ptBrCulture },
    SupportedUICultures = new List<CultureInfo> { ptBrCulture }
};

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Account/AccessDenied");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRequestLocalization(localizationOptions);

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
    pattern: "{controller=AdminHome}/{action=Index}/{id?}");

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
    var imageSources = new List<string> { "'self'", "data:", "blob:", "https://ui-avatars.com" };
    var mediaSources = new List<string> { "'self'", "data:", "blob:", "https://ui-avatars.com" };
    connectSources.Add("https://cdnjs.cloudflare.com");
    imageSources.Add("https://tile.openstreetmap.org");
    imageSources.Add("https://*.tile.openstreetmap.org");

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
            "font-src 'self' https://cdnjs.cloudflare.com;",
            "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com;",
            "script-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com;"
        });
}
