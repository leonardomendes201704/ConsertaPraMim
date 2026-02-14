using ConsertaPraMim.Infrastructure;
using ConsertaPraMim.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Services;
using ConsertaPraMim.Web.Provider.Options;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

// Clean Architecture Layers
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddHttpClient();
builder.Services.AddScoped<INotificationService, ApiNotificationService>();
var apiOrigin = ResolveOrigin(builder.Configuration["ApiBaseUrl"]);

// Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ConsertaPraMim.Provider.Auth";
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});
builder.Services.Configure<LegacyAdminOptions>(builder.Configuration.GetSection(LegacyAdminOptions.SectionName));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
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
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true &&
        context.User.IsInRole("Provider"))
    {
        var userIdRaw = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdRaw, out _))
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Response.Redirect("/Account/Login");
            return;
        }
    }

    if (context.User.Identity?.IsAuthenticated == true &&
        context.User.IsInRole("Provider") &&
        !IsOnboardingExemptPath(context.Request.Path))
    {
        var userIdRaw = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdRaw, out var userId))
        {
            var onboardingService = context.RequestServices.GetRequiredService<IProviderOnboardingService>();
            var onboardingState = await onboardingService.GetStateAsync(userId);
            if (onboardingState == null)
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Response.Redirect("/Account/Login");
                return;
            }

            var onboardingCompleted = onboardingState.IsCompleted || onboardingState.Status == ProviderOnboardingStatus.Active;
            if (!onboardingCompleted)
            {
                context.Response.Redirect("/Onboarding");
                return;
            }
        }
        else
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Response.Redirect("/Account/Login");
            return;
        }
    }

    await next();
});
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ConsertaPraMim.Infrastructure.Hubs.NotificationHub>("/notificationHub");
app.MapHub<ConsertaPraMim.Infrastructure.Hubs.ChatHub>("/chatHub");

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

    connectSources.Add("https://cdnjs.cloudflare.com");
    connectSources.Add("https://tile.openstreetmap.org");
    connectSources.Add("https://*.tile.openstreetmap.org");
    imageSources.Add("https://cdnjs.cloudflare.com");
    imageSources.Add("https://tile.openstreetmap.org");
    imageSources.Add("https://*.tile.openstreetmap.org");

    if (isDevelopment)
    {
        connectSources.AddRange(new[]
        {
            "http://localhost:*",
            "https://localhost:*",
            "ws://localhost:*",
            "wss://localhost:*",
            "http://127.0.0.1:*",
            "https://127.0.0.1:*",
            "ws://127.0.0.1:*",
            "wss://127.0.0.1:*"
        });
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

static bool IsOnboardingExemptPath(PathString path)
{
    return path.StartsWithSegments("/Onboarding", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/Account/Login", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/Account/Register", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/Account/Logout", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/notificationHub", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/chatHub", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/Home/Error", StringComparison.OrdinalIgnoreCase);
}
