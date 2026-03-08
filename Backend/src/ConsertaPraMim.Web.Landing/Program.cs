using System.Globalization;
using ConsertaPraMim.Web.Landing.Models;
using ConsertaPraMim.Web.Landing.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var ptBrCulture = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = ptBrCulture;
CultureInfo.DefaultThreadCurrentUICulture = ptBrCulture;

builder.Services.AddControllersWithViews();
builder.Services.Configure<LandingSiteOptions>(builder.Configuration.GetSection(LandingSiteOptions.SectionName));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var landingSiteOptions = builder.Configuration.GetSection(LandingSiteOptions.SectionName).Get<LandingSiteOptions>() ?? new LandingSiteOptions();
var configuredApiBaseUrl = LandingSiteOptions.ResolveApiBaseUrl(
    landingSiteOptions.ApiBaseUrl,
    landingSiteOptions.ApiSwaggerUrl,
    "https://api.consertapramim.com");

var app = builder.Build();
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(ptBrCulture),
    SupportedCultures = new List<CultureInfo> { ptBrCulture },
    SupportedUICultures = new List<CultureInfo> { ptBrCulture }
};

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRequestLocalization(localizationOptions);

app.Use(async (context, next) =>
{
    var resolvedApiBaseUrl = LandingPublicUrlResolver.ResolveApiBaseUrl(
        configuredApiBaseUrl,
        context.Request.Host.Host,
        "https://api.consertapramim.com");

    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] = BuildContentSecurityPolicy(resolvedApiBaseUrl, app.Environment.IsDevelopment());

    await next();
});

app.UseRouting();
app.UseAuthorization();

app.MapGet("/health", () => Results.Text("Healthy", "text/plain"));
app.MapGet("/robots.txt", (IOptions<LandingSiteOptions> options) =>
{
    var canonicalUrl = LandingSiteOptions.NormalizeUrl(options.Value.CanonicalUrl, "https://www.consertapramim.com");
    var sitemapUrl = canonicalUrl.TrimEnd('/') + "/sitemap.xml";
    var content = $"User-agent: *\nAllow: /\n\nSitemap: {sitemapUrl}\n";
    return Results.Text(content, "text/plain");
});
app.MapGet("/sitemap.xml", (IOptions<LandingSiteOptions> options) =>
{
    var canonicalUrl = LandingSiteOptions.NormalizeUrl(options.Value.CanonicalUrl, "https://www.consertapramim.com").TrimEnd('/');
    var escapedUrl = System.Security.SecurityElement.Escape(canonicalUrl) ?? canonicalUrl;
    var content =
        $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n" +
        "  <url>\n" +
        $"    <loc>{escapedUrl}/</loc>\n" +
        "  </url>\n" +
        "</urlset>\n";
    return Results.Content(content, "application/xml");
});
app.MapControllerRoute(
    name: "landing-client",
    pattern: "Cliente",
    defaults: new { controller = "Home", action = "Cliente" });
app.MapControllerRoute(
    name: "landing-provider",
    pattern: "Prestador",
    defaults: new { controller = "Home", action = "Prestador" });
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static string BuildContentSecurityPolicy(string apiBaseUrl, bool isDevelopment)
{
    var connectSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "'self'"
    };

    var apiOrigin = LandingSiteOptions.ResolveOrigin(apiBaseUrl);
    if (!string.IsNullOrWhiteSpace(apiOrigin))
    {
        connectSources.Add(apiOrigin);
    }

    if (isDevelopment)
    {
        connectSources.Add("http:");
        connectSources.Add("https:");
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
            $"connect-src {string.Join(' ', connectSources)};",
            "img-src 'self' data:;",
            "font-src 'self';",
            "style-src 'self';",
            "script-src 'self';"
        });
}
