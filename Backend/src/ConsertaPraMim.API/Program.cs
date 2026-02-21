using ConsertaPraMim.Infrastructure;
using ConsertaPraMim.Application;
using ConsertaPraMim.API.BackgroundJobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FluentValidation.AspNetCore;
using ConsertaPraMim.Infrastructure.Hubs;
using Microsoft.Extensions.FileProviders;
using System.Security.Claims;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.API.Middleware;
using ConsertaPraMim.API.Services;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc;
using ConsertaPraMim.Infrastructure.Configuration;
//teste de deploy automatico
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddSystemSettingsOverridesFromDatabase();

// Add services to the container.
builder.Services.AddControllers()
    .AddFluentValidation(fv => fv.AutomaticValidationEnabled = true);
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var warningCollector = context.HttpContext.RequestServices.GetService<IRequestWarningCollector>();
        warningCollector?.AddWarning("validation_error");

        var validationProblem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Requisicao invalida.",
            Detail = "Um ou mais campos estao invalidos."
        };

        return new BadRequestObjectResult(validationProblem);
    };
});
builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
    { 
        Title = "ConsertaPraMim API", 
        Version = "v1",
        Description = "API do sistema ConsertaPraMim - Conectando clientes a prestadores de serviços."
    });

    // Add JWT support in Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Insira o token JWT desta forma: Bearer {seu_token}"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });

    // Use XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

// Clean Architecture Layers
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddHostedService<ServiceAppointmentExpirationWorker>();
builder.Services.AddHostedService<ServiceScopeChangeExpirationWorker>();
builder.Services.AddHostedService<ServiceAppointmentReminderWorker>();
builder.Services.AddHostedService<ServiceAppointmentNoShowRiskWorker>();
builder.Services.AddHostedService<ServiceWarrantyClaimSlaWorker>();
builder.Services.AddHostedService<ProviderGalleryEvidenceRetentionWorker>();
builder.Services.AddHostedService<DatabaseKeepAliveWorker>();
builder.Services.AddHostedService<ApiRequestTelemetryFlushWorker>();
builder.Services.AddHostedService<ApiMonitoringAggregationWorker>();
builder.Services.AddSingleton<IAdminMonitoringRealtimeNotifier, AdminMonitoringRealtimeNotifier>();

ICorsRuntimeSettings? corsRuntimeSettings = null;
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebApps", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
              {
                  if (corsRuntimeSettings?.IsOriginAllowed(origin) == true)
                  {
                      return true;
                  }

                  if (!builder.Environment.IsDevelopment())
                  {
                      return false;
                  }

                  if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                  {
                      return false;
                  }

                  var isHttpScheme = uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                                     uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

                  if (!isHttpScheme)
                  {
                      return false;
                  }

                  if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                      uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                      uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase))
                  {
                      return true;
                  }

                  if (!IPAddress.TryParse(uri.Host, out var ipAddress))
                  {
                      return false;
                  }

                  if (IPAddress.IsLoopback(ipAddress))
                  {
                      return true;
                  }

                  if (ipAddress.AddressFamily != AddressFamily.InterNetwork)
                  {
                      return false;
                  }

                  var octets = ipAddress.GetAddressBytes();
                  var isPrivateNetwork =
                      octets[0] == 10 ||
                      (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31) ||
                      (octets[0] == 192 && octets[1] == 168);

                  return isPrivateNetwork;
              })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
{
    throw new InvalidOperationException("JwtSettings:SecretKey nao configurada ou invalida. Configure uma chave com no minimo 32 caracteres.");
}
var key = Encoding.ASCII.GetBytes(secretKey);
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];
if (!builder.Environment.IsDevelopment() &&
    (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience)))
{
    throw new InvalidOperationException("JwtSettings:Issuer e JwtSettings:Audience devem ser configurados fora de Development.");
}

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
        ValidIssuer = issuer,
        ValidateAudience = !string.IsNullOrWhiteSpace(audience),
        ValidAudience = audience,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
    x.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrWhiteSpace(accessToken) &&
                (path.StartsWithSegments("/notificationHub") ||
                 path.StartsWithSegments("/chatHub") ||
                 path.StartsWithSegments("/adminMonitoringHub")))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();
corsRuntimeSettings = app.Services.GetRequiredService<ICorsRuntimeSettings>();
var swaggerEnabledInProduction = builder.Configuration.GetValue<bool>("Swagger:EnabledInProduction");

// Seed Database (centralized)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await ConsertaPraMim.Infrastructure.Data.DbInitializer.SeedAsync(services);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || swaggerEnabledInProduction)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseMiddleware<CorrelationIdMiddleware>();
var webRootPath = app.Environment.WebRootPath;
if (string.IsNullOrWhiteSpace(webRootPath))
{
    webRootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
}

Directory.CreateDirectory(webRootPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRootPath)
});
app.UseCors("WebApps");
app.UseAuthentication();
app.UseMiddleware<RequestTelemetryMiddleware>();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true &&
        context.User.IsInRole("Provider") &&
        !IsProviderOnboardingExemptPath(context.Request.Path))
    {
        var userIdRaw = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdRaw, out var userId))
        {
            var onboardingService = context.RequestServices.GetRequiredService<IProviderOnboardingService>();
            var onboardingComplete = await onboardingService.IsOnboardingCompleteAsync(userId);
            if (!onboardingComplete)
            {
                context.Response.StatusCode = StatusCodes.Status423Locked;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Onboarding pendente. Conclua o wizard para acessar este recurso."
                });
                return;
            }
        }
    }

    await next();
});
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHub<NotificationHub>("/notificationHub");
app.MapHub<ChatHub>("/chatHub");
app.MapHub<AdminMonitoringHub>("/adminMonitoringHub");

app.Run();

static bool IsProviderOnboardingExemptPath(PathString path)
{
    return path.StartsWithSegments("/api/provider-onboarding", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/notificationHub", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/chatHub", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/adminMonitoringHub", StringComparison.OrdinalIgnoreCase);
}

public partial class Program
{
}
