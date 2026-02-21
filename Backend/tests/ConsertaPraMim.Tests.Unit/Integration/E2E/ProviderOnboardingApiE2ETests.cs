using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Tests.Unit.Integration.E2E;

public class ProviderOnboardingApiE2ETests
{
    [Fact(DisplayName = "Prestador onboarding api e 2 e | Obter state e salvar plan | Deve work end para end")]
    public async Task GetState_And_SavePlan_ShouldWork_EndToEnd()
    {
        await using var factory = new ProviderOnboardingApiFactory();
        var providerId = await factory.SeedProviderAsync();

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        ConfigureAuthHeaders(client, providerId);

        var stateResponse = await client.GetAsync("/api/provider-onboarding");
        Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);

        var state = await stateResponse.Content.ReadFromJsonAsync<ProviderOnboardingStateDto>();
        Assert.NotNull(state);
        Assert.Equal(ProviderPlan.Trial, state.SelectedPlan);
        Assert.False(state.PlanCompleted);

        var invalidPlanResponse = await client.PutAsJsonAsync(
            "/api/provider-onboarding/plan",
            new SaveProviderOnboardingPlanDto(ProviderPlan.Trial));
        Assert.Equal(HttpStatusCode.BadRequest, invalidPlanResponse.StatusCode);

        var validPlanResponse = await client.PutAsJsonAsync(
            "/api/provider-onboarding/plan",
            new SaveProviderOnboardingPlanDto(ProviderPlan.Bronze));
        Assert.Equal(HttpStatusCode.NoContent, validPlanResponse.StatusCode);

        stateResponse = await client.GetAsync("/api/provider-onboarding");
        Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);

        state = await stateResponse.Content.ReadFromJsonAsync<ProviderOnboardingStateDto>();
        Assert.NotNull(state);
        Assert.Equal(ProviderPlan.Bronze, state.SelectedPlan);
        Assert.True(state.PlanCompleted);
    }

    [Fact(DisplayName = "Prestador onboarding api e 2 e | Upload documents e complete | Deve succeed quando required documents sent")]
    public async Task UploadDocuments_And_Complete_ShouldSucceed_WhenRequiredDocumentsAreSent()
    {
        await using var factory = new ProviderOnboardingApiFactory();
        var providerId = await factory.SeedProviderAsync();

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        ConfigureAuthHeaders(client, providerId);

        var basicDataResponse = await client.PutAsJsonAsync(
            "/api/provider-onboarding/basic-data",
            new UpdateProviderOnboardingBasicDataDto("Prestador E2E Atualizado", "11912345678"));
        Assert.Equal(HttpStatusCode.NoContent, basicDataResponse.StatusCode);

        var planResponse = await client.PutAsJsonAsync(
            "/api/provider-onboarding/plan",
            new SaveProviderOnboardingPlanDto(ProviderPlan.Silver));
        Assert.Equal(HttpStatusCode.NoContent, planResponse.StatusCode);

        var identityUpload = await UploadDocumentAsync(
            client,
            ProviderDocumentType.IdentityDocument,
            "application/pdf",
            "../../rg%?.pdf",
            new byte[] { 1, 2, 3, 4, 5 });
        Assert.Equal(HttpStatusCode.OK, identityUpload.StatusCode);
        var identityDoc = await identityUpload.Content.ReadFromJsonAsync<ProviderOnboardingDocumentDto>();
        Assert.NotNull(identityDoc);
        Assert.Equal(ProviderDocumentType.IdentityDocument, identityDoc.DocumentType);
        Assert.Equal("rg__.pdf", identityDoc.FileName);

        var selfieUpload = await UploadDocumentAsync(
            client,
            ProviderDocumentType.SelfieWithDocument,
            "image/png",
            "selfie.png",
            new byte[] { 9, 8, 7, 6, 5, 4 });
        Assert.Equal(HttpStatusCode.OK, selfieUpload.StatusCode);
        var selfieDoc = await selfieUpload.Content.ReadFromJsonAsync<ProviderOnboardingDocumentDto>();
        Assert.NotNull(selfieDoc);
        Assert.Equal(ProviderDocumentType.SelfieWithDocument, selfieDoc.DocumentType);

        var completeResponse = await client.PostAsync("/api/provider-onboarding/complete", content: null);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var completeResult = await completeResponse.Content.ReadFromJsonAsync<CompleteProviderOnboardingResult>();
        Assert.NotNull(completeResult);
        Assert.True(completeResult.Success);

        var finalStateResponse = await client.GetAsync("/api/provider-onboarding");
        Assert.Equal(HttpStatusCode.OK, finalStateResponse.StatusCode);
        var finalState = await finalStateResponse.Content.ReadFromJsonAsync<ProviderOnboardingStateDto>();
        Assert.NotNull(finalState);
        Assert.True(finalState.IsCompleted);
        Assert.Equal(ProviderOnboardingStatus.PendingApproval, finalState.Status);
        Assert.True(finalState.DocumentsCompleted);
    }

    [Fact(DisplayName = "Prestador onboarding api e 2 e | Upload document | Deve retornar invalida requisicao quando mime type invalido")]
    public async Task UploadDocument_ShouldReturnBadRequest_WhenMimeTypeIsInvalid()
    {
        await using var factory = new ProviderOnboardingApiFactory();
        var providerId = await factory.SeedProviderAsync();

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        ConfigureAuthHeaders(client, providerId);

        var response = await UploadDocumentAsync(
            client,
            ProviderDocumentType.IdentityDocument,
            "text/plain",
            "doc.pdf",
            new byte[] { 1, 2, 3 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> UploadDocumentAsync(
        HttpClient client,
        ProviderDocumentType documentType,
        string contentType,
        string fileName,
        byte[] bytes)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(((int)documentType).ToString()), "DocumentType");

        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(fileContent, "File", fileName);

        return await client.PostAsync("/api/provider-onboarding/documents", content);
    }

    private static void ConfigureAuthHeaders(HttpClient client, Guid providerId)
    {
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, providerId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, UserRole.Provider.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeaderName, "prestador.e2e@teste.com");
    }

    private sealed class ProviderOnboardingApiFactory : WebApplicationFactory<Program>
    {
        private SqliteConnection? _connection;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Seed:Enabled"] = "false",
                    ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=ConsertaPraMim.Tests;Trusted_Connection=True;"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ConsertaPraMimDbContext>>();
                services.RemoveAll<ConsertaPraMimDbContext>();
                var dbContextOptionConfigurations = services
                    .Where(descriptor =>
                        descriptor.ServiceType.IsGenericType &&
                        descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>) &&
                        descriptor.ServiceType.GenericTypeArguments[0] == typeof(ConsertaPraMimDbContext))
                    .ToList();
                foreach (var descriptor in dbContextOptionConfigurations)
                {
                    services.Remove(descriptor);
                }

                var backgroundWorkers = services
                    .Where(descriptor =>
                        descriptor.ServiceType == typeof(IHostedService) &&
                        descriptor.ImplementationType?.Namespace == "ConsertaPraMim.API.BackgroundJobs")
                    .ToList();
                foreach (var descriptor in backgroundWorkers)
                {
                    services.Remove(descriptor);
                }

                services.RemoveAll<IFileStorageService>();
                services.AddSingleton<IFileStorageService, InMemoryFileStorageService>();

                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName,
                        _ => { });

                _connection = new SqliteConnection("DataSource=:memory:");
                _connection.Open();

                services.AddSingleton(_connection);
                services.AddDbContext<ConsertaPraMimDbContext>((serviceProvider, options) =>
                {
                    options.UseSqlite(serviceProvider.GetRequiredService<SqliteConnection>());
                });

                using var serviceScope = services.BuildServiceProvider().CreateScope();
                var dbContext = serviceScope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();
                dbContext.Database.EnsureCreated();
            });
        }

        public async Task<Guid> SeedProviderAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();

            var providerId = Guid.NewGuid();
            var provider = new User
            {
                Id = providerId,
                Name = "Prestador E2E",
                Email = "prestador.e2e@teste.com",
                PasswordHash = "hash",
                Phone = "11900000000",
                Role = UserRole.Provider,
                IsActive = true,
                ProviderProfile = new ProviderProfile
                {
                    UserId = providerId,
                    Plan = ProviderPlan.Trial,
                    OnboardingStatus = ProviderOnboardingStatus.PendingDocumentation,
                    IsOnboardingCompleted = false,
                    RadiusKm = 5,
                    Categories = new List<ServiceCategory>
                    {
                        ServiceCategory.Electrical
                    }
                }
            };

            context.Users.Add(provider);
            await context.SaveChangesAsync();
            return providerId;
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }
        }
    }

    private sealed class InMemoryFileStorageService : IFileStorageService
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

        public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string folder)
        {
            await using var memory = new MemoryStream();
            await fileStream.CopyToAsync(memory);

            var key = $"/uploads/{folder}/{Guid.NewGuid():N}-{fileName}";
            _files[key] = memory.ToArray();
            return key;
        }

        public void DeleteFile(string filePath)
        {
            _files.Remove(filePath);
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "OnboardingTestAuth";
        public const string UserIdHeaderName = "X-Test-UserId";
        public const string RoleHeaderName = "X-Test-Role";
        public const string EmailHeaderName = "X-Test-Email";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var userIdRaw = Request.Headers[UserIdHeaderName].FirstOrDefault();
            var role = Request.Headers[RoleHeaderName].FirstOrDefault();
            var email = Request.Headers[EmailHeaderName].FirstOrDefault() ?? "test@localhost";

            if (!Guid.TryParse(userIdRaw, out var userId) || string.IsNullOrWhiteSpace(role))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing test authentication headers."));
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim(ClaimTypes.Email, email)
            };
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
