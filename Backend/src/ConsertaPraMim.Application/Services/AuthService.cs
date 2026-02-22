using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

namespace ConsertaPraMim.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ILegalTermsRepository _legalTermsRepository;
    private readonly IConfiguration _configuration;
    private readonly IAdminAuditLogRepository? _adminAuditLogRepository;
    private readonly IAdminOperationalEventNotifier _adminOperationalEventNotifier;

    public AuthService(
        IUserRepository userRepository,
        ILegalTermsRepository legalTermsRepository,
        IConfiguration configuration,
        IAdminAuditLogRepository? adminAuditLogRepository = null,
        IAdminOperationalEventNotifier? adminOperationalEventNotifier = null)
    {
        _userRepository = userRepository;
        _legalTermsRepository = legalTermsRepository;
        _configuration = configuration;
        _adminAuditLogRepository = adminAuditLogRepository;
        _adminOperationalEventNotifier = adminOperationalEventNotifier ?? NullAdminOperationalEventNotifier.Instance;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null) return null;
        
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) return null;

        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        if (user.Role is UserRole.Client or UserRole.Provider)
        {
            await TryWriteLoginAuditAsync(user);
            await _adminOperationalEventNotifier.NotifyUserLoggedInAsync(
                user.Id,
                user.Name,
                user.Role.ToString());
        }

        var token = GenerateJwtToken(user);
        return new LoginResponse(user.Id, token, user.Name, user.Role.ToString(), user.Email);
    }

    public async Task<LoginResponse?> RegisterAsync(RegisterRequest request)
    {
        if (await _userRepository.GetByEmailAsync(request.Email) != null)
            return null; // User exists

        if (!Enum.IsDefined(typeof(UserRole), request.Role))
            return null;

        var requestedRole = (UserRole)request.Role;
        if (requestedRole is not (UserRole.Client or UserRole.Provider))
            return null;

        var expectedAudience = requestedRole == UserRole.Client
            ? LegalTermsAudience.Client
            : LegalTermsAudience.Provider;

        if (!LegalTermsService.TryParseAudience(request.TermsType, out var requestedAudience) ||
            requestedAudience != expectedAudience ||
            !request.TermsAccepted ||
            request.TermsVersion <= 0)
        {
            return null;
        }

        var activeTerms = await _legalTermsRepository.GetActiveByAudienceAsync(expectedAudience);
        if (activeTerms == null || !activeTerms.IsPublished || activeTerms.Version != request.TermsVersion)
        {
            return null;
        }
             
        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Phone = request.Phone,
            Role = requestedRole,
            IsActive = true
        };

        if (requestedRole == UserRole.Provider)
        {
            user.ProviderProfile = new ProviderProfile
            {
                UserId = user.Id,
                Plan = ProviderPlan.Trial,
                OnboardingStatus = ProviderOnboardingStatus.PendingDocumentation,
                IsOnboardingCompleted = false,
                OnboardingStartedAt = DateTime.UtcNow
            };
        }

        await _userRepository.AddAsync(user);
        await _legalTermsRepository.AddAcceptanceAsync(new UserLegalTermsAcceptance
        {
            UserId = user.Id,
            LegalTermsDocumentId = activeTerms.Id,
            Audience = expectedAudience,
            TermsVersion = activeTerms.Version,
            AcceptedAtUtc = DateTime.UtcNow,
            Source = NormalizeTermsAcceptanceSource(request.TermsAcceptanceSource, requestedRole),
            MetadataJson = JsonSerializer.Serialize(new
            {
                termsType = LegalTermsService.ToAudienceKey(expectedAudience),
                termsVersion = activeTerms.Version
            })
        });
        await _legalTermsRepository.SaveChangesAsync();

        if (requestedRole is UserRole.Client or UserRole.Provider)
        {
            await _adminOperationalEventNotifier.NotifyUserRegisteredAsync(
                user.Id,
                user.Name,
                user.Role.ToString());
        }

        var token = GenerateJwtToken(user);
        return new LoginResponse(user.Id, token, user.Name, user.Role.ToString(), user.Email);
    }

    private async Task TryWriteLoginAuditAsync(User user)
    {
        if (_adminAuditLogRepository == null || user.Id == Guid.Empty)
        {
            return;
        }

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = user.Id,
            ActorEmail = user.Email,
            Action = "user_login",
            TargetType = "UserAuth",
            TargetId = user.Id,
            Metadata = $"role={user.Role}"
        });
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
        {
            throw new InvalidOperationException("JwtSettings:SecretKey nao configurada ou invalida. Configure uma chave com no minimo 32 caracteres.");
        }
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];

        var key = Encoding.ASCII.GetBytes(secretKey);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            Issuer = string.IsNullOrWhiteSpace(issuer) ? null : issuer,
            Audience = string.IsNullOrWhiteSpace(audience) ? null : audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static string NormalizeTermsAcceptanceSource(string? source, UserRole role)
    {
        var fallback = role == UserRole.Client
            ? "web_client"
            : "web_provider";

        if (string.IsNullOrWhiteSpace(source))
        {
            return fallback;
        }

        var trimmed = source.Trim();
        if (trimmed.Length <= 60)
        {
            return trimmed;
        }

        return trimmed[..60];
    }

    private sealed class NullAdminOperationalEventNotifier : IAdminOperationalEventNotifier
    {
        public static readonly NullAdminOperationalEventNotifier Instance = new();

        public Task NotifyClientOpenedRequestAsync(Guid requestId, string? requestDescription, string? categoryName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyProviderOpenedSupportTicketAsync(Guid ticketId, Guid providerUserId, string? ticketSubject, string? categoryName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyProviderSentProposalAsync(Guid proposalId, Guid requestId, decimal? estimatedValue, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyClientAcceptedProposalAsync(Guid proposalId, Guid requestId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyClientScheduledAsync(Guid appointmentId, Guid requestId, DateTime windowStartUtc, DateTime windowEndUtc, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyUserRegisteredAsync(Guid userId, string userName, string role, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyUserLoggedInAsync(Guid userId, string userName, string role, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
