using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Application.Services;

public sealed class LandingAdminNotificationService : ILandingAdminNotificationService
{
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<LandingAdminNotificationService> _logger;

    public LandingAdminNotificationService(
        IUserRepository userRepository,
        INotificationService notificationService,
        ILogger<LandingAdminNotificationService> logger)
    {
        _userRepository = userRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public Task NotifyLandingAccessAsync(
        NotifyLandingAccessRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedPath = NormalizeSnippet(request.Path, fallback: "/");
        var accessLabel = ResolveAccessLabel(normalizedPath, request.InitialLeadOrigin);
        var ipSnippet = NormalizeSnippet(FirstNonEmpty(request.IpAddress, request.ForwardedFor), fallback: "IP nao informado", maxLength: 80);
        var agentSnippet = NormalizeSnippet(request.UserAgent, fallback: "user-agent nao informado", maxLength: 110);
        var message = $"{accessLabel} | {ipSnippet} | {agentSnippet}";

        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "landing_public_access",
            ["visitorId"] = NormalizePayloadValue(request.VisitorId),
            ["path"] = normalizedPath,
            ["ipAddress"] = NormalizePayloadValue(request.IpAddress),
            ["forwardedFor"] = NormalizePayloadValue(request.ForwardedFor),
            ["userAgent"] = NormalizePayloadValue(request.UserAgent),
            ["acceptLanguage"] = NormalizePayloadValue(request.AcceptLanguage),
            ["host"] = NormalizePayloadValue(request.Host),
            ["scheme"] = NormalizePayloadValue(request.Scheme),
            ["currentUrl"] = NormalizePayloadValue(request.CurrentUrl),
            ["refererUrl"] = NormalizePayloadValue(request.RefererUrl),
            ["initialLeadOrigin"] = NormalizePayloadValue(request.InitialLeadOrigin)
        };

        return NotifyAllAdminsAsync(
            title: "Novo acesso na landing",
            message: message,
            actionUrl: "/AdminHome/Index",
            payload,
            cancellationToken);
    }

    public Task NotifyLandingLeadCapturedAsync(
        LandingLead lead,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lead);

        var originLabel = lead.Origin == LandingLeadOrigin.Client ? "cliente" : "prestador";
        var title = lead.Origin == LandingLeadOrigin.Client
            ? "Novo lead de cliente na landing"
            : "Novo lead de prestador na landing";
        var nameSnippet = NormalizeSnippet(lead.FullName, fallback: "Lead sem nome", maxLength: 70);
        var locationSnippet = NormalizeSnippet(BuildLocation(lead.Neighborhood, lead.City, lead.State), fallback: "Localidade nao informada", maxLength: 90);
        var contextSnippet = NormalizeSnippet(
            FirstNonEmpty(
                lead.RequestedService,
                lead.ServiceCategory,
                lead.CompanyName,
                lead.Message),
            fallback: "Contexto comercial nao informado",
            maxLength: 120);

        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "landing_lead_captured",
            ["leadId"] = lead.Id.ToString("N"),
            ["origin"] = originLabel,
            ["visitorId"] = NormalizePayloadValue(lead.VisitorId),
            ["fullName"] = NormalizePayloadValue(lead.FullName),
            ["phone"] = NormalizePayloadValue(lead.Phone),
            ["email"] = NormalizePayloadValue(lead.Email),
            ["city"] = NormalizePayloadValue(lead.City),
            ["state"] = NormalizePayloadValue(lead.State),
            ["neighborhood"] = NormalizePayloadValue(lead.Neighborhood),
            ["serviceCategory"] = NormalizePayloadValue(lead.ServiceCategory),
            ["requestedService"] = NormalizePayloadValue(lead.RequestedService),
            ["companyName"] = NormalizePayloadValue(lead.CompanyName),
            ["ipAddress"] = NormalizePayloadValue(lead.IpAddress),
            ["forwardedFor"] = NormalizePayloadValue(lead.ForwardedFor),
            ["userAgent"] = NormalizePayloadValue(lead.UserAgent)
        };

        return NotifyAllAdminsAsync(
            title,
            $"{nameSnippet} | {locationSnippet} | {contextSnippet}",
            $"/AdminLandingLeads/Details/{lead.Id}",
            payload,
            cancellationToken);
    }

    private async Task NotifyAllAdminsAsync(
        string title,
        string message,
        string? actionUrl,
        IReadOnlyDictionary<string, string> payload,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var admins = (await _userRepository.GetAllAsync())
            .Where(user => user.IsActive && user.Role == UserRole.Admin)
            .ToList();

        if (admins.Count == 0)
        {
            _logger.LogDebug("Nenhum admin ativo disponivel para receber notificacao da landing.");
            return;
        }

        var tasks = admins.Select(async admin =>
        {
            try
            {
                await _notificationService.SendNotificationAsync(
                    admin.Id.ToString("N"),
                    title,
                    message,
                    actionUrl,
                    payload);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Falha ao enviar notificacao da landing para admin {AdminUserId}.",
                    admin.Id);
            }
        });

        await Task.WhenAll(tasks);
    }

    private static string ResolveAccessLabel(string normalizedPath, string? initialLeadOrigin)
    {
        if (normalizedPath.Equals("/Prestador", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(initialLeadOrigin, "provider", StringComparison.OrdinalIgnoreCase))
        {
            return "Landing /Prestador";
        }

        if (normalizedPath.Equals("/Cliente", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(initialLeadOrigin, "client", StringComparison.OrdinalIgnoreCase))
        {
            return "Landing /Cliente";
        }

        return "Landing /";
    }

    private static string BuildLocation(string? neighborhood, string? city, string? state)
    {
        var cityState = string.IsNullOrWhiteSpace(city)
            ? null
            : string.IsNullOrWhiteSpace(state)
                ? city.Trim()
                : $"{city.Trim()}/{state.Trim().ToUpperInvariant()}";

        if (!string.IsNullOrWhiteSpace(neighborhood) && !string.IsNullOrWhiteSpace(cityState))
        {
            return $"{neighborhood.Trim()} - {cityState}";
        }

        return FirstNonEmpty(neighborhood, cityState) ?? string.Empty;
    }

    private static string NormalizePayloadValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private static string NormalizeSnippet(string? value, string fallback, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : $"{trimmed[..maxLength]}...";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
