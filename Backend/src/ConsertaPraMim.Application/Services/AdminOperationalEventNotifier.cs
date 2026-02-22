using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Application.Services;

public class AdminOperationalEventNotifier : IAdminOperationalEventNotifier
{
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AdminOperationalEventNotifier> _logger;

    public AdminOperationalEventNotifier(
        IUserRepository userRepository,
        INotificationService notificationService,
        ILogger<AdminOperationalEventNotifier> logger)
    {
        _userRepository = userRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public Task NotifyClientOpenedRequestAsync(
        Guid requestId,
        string? requestDescription,
        string? categoryName,
        CancellationToken cancellationToken = default)
    {
        var title = "Cliente abriu um pedido";
        var descriptionSnippet = NormalizeSnippet(requestDescription, fallback: "Pedido sem descricao");
        var message = $"Categoria: {NormalizeSnippet(categoryName, fallback: "Nao informada")} | {descriptionSnippet}";
        return NotifyAllAdminsAsync(
            title,
            message,
            type: "admin_event_client_opened_request",
            actionUrl: "/AdminHome/Index",
            data: new Dictionary<string, string>
            {
                ["requestId"] = requestId.ToString("N")
            },
            cancellationToken);
    }

    public Task NotifyProviderSentProposalAsync(
        Guid proposalId,
        Guid requestId,
        decimal? estimatedValue,
        CancellationToken cancellationToken = default)
    {
        var message = estimatedValue.HasValue && estimatedValue.Value > 0m
            ? $"Pedido {requestId:N} | Valor estimado: R$ {estimatedValue.Value:0.00}"
            : $"Pedido {requestId:N}";
        return NotifyAllAdminsAsync(
            "Prestador enviou uma proposta",
            message,
            type: "admin_event_provider_sent_proposal",
            actionUrl: "/AdminHome/Index",
            data: new Dictionary<string, string>
            {
                ["proposalId"] = proposalId.ToString("N"),
                ["requestId"] = requestId.ToString("N")
            },
            cancellationToken);
    }

    public Task NotifyClientAcceptedProposalAsync(
        Guid proposalId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        return NotifyAllAdminsAsync(
            "Cliente aceitou a proposta",
            $"Pedido {requestId:N}",
            type: "admin_event_client_accepted_proposal",
            actionUrl: "/AdminHome/Index",
            data: new Dictionary<string, string>
            {
                ["proposalId"] = proposalId.ToString("N"),
                ["requestId"] = requestId.ToString("N")
            },
            cancellationToken);
    }

    public Task NotifyClientScheduledAsync(
        Guid appointmentId,
        Guid requestId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        CancellationToken cancellationToken = default)
    {
        var message = $"Pedido {requestId:N} | Janela: {windowStartUtc:dd/MM HH:mm} - {windowEndUtc:HH:mm} UTC";
        return NotifyAllAdminsAsync(
            "Cliente agendou atendimento",
            message,
            type: "admin_event_client_scheduled",
            actionUrl: "/AdminHome/Index",
            data: new Dictionary<string, string>
            {
                ["appointmentId"] = appointmentId.ToString("N"),
                ["requestId"] = requestId.ToString("N")
            },
            cancellationToken);
    }

    public Task NotifyUserRegisteredAsync(
        Guid userId,
        string userName,
        string role,
        CancellationToken cancellationToken = default)
    {
        var normalizedRole = NormalizeRole(role);
        var title = normalizedRole == "client"
            ? "Cliente novo cadastrado"
            : normalizedRole == "provider"
                ? "Prestador novo cadastrado"
                : "Novo usuario cadastrado";
        var type = normalizedRole == "client"
            ? "admin_event_client_registered"
            : normalizedRole == "provider"
                ? "admin_event_provider_registered"
                : "admin_event_user_registered";
        var message = NormalizeSnippet(userName, fallback: "Usuario sem nome");
        return NotifyAllAdminsAsync(
            title,
            message,
            type,
            actionUrl: "/AdminUsers/Index",
            data: new Dictionary<string, string>
            {
                ["userId"] = userId.ToString("N"),
                ["role"] = normalizedRole
            },
            cancellationToken);
    }

    public Task NotifyUserLoggedInAsync(
        Guid userId,
        string userName,
        string role,
        CancellationToken cancellationToken = default)
    {
        var normalizedRole = NormalizeRole(role);
        var title = normalizedRole == "client"
            ? "Cliente fez login"
            : normalizedRole == "provider"
                ? "Prestador fez login"
                : "Usuario fez login";
        var type = normalizedRole == "client"
            ? "admin_event_client_login"
            : normalizedRole == "provider"
                ? "admin_event_provider_login"
                : "admin_event_user_login";
        var message = NormalizeSnippet(userName, fallback: "Usuario sem nome");
        return NotifyAllAdminsAsync(
            title,
            message,
            type,
            actionUrl: "/AdminHome/Index",
            data: new Dictionary<string, string>
            {
                ["userId"] = userId.ToString("N"),
                ["role"] = normalizedRole
            },
            cancellationToken);
    }

    private async Task NotifyAllAdminsAsync(
        string title,
        string message,
        string type,
        string? actionUrl,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var admins = (await _userRepository.GetAllAsync())
            .Where(u => u.IsActive && u.Role == UserRole.Admin)
            .ToList();

        if (admins.Count == 0)
        {
            _logger.LogDebug("Nenhum admin ativo para notificar evento operacional: {Type}", type);
            return;
        }

        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = type
        };
        if (data != null)
        {
            foreach (var pair in data)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                {
                    continue;
                }

                payload[pair.Key.Trim()] = pair.Value.Trim();
            }
        }

        var tasks = admins.Select(admin => _notificationService.SendNotificationAsync(
            admin.Id.ToString("N"),
            title,
            message,
            actionUrl,
            payload));

        await Task.WhenAll(tasks);
    }

    private static string NormalizeRole(string role)
    {
        var normalized = role?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized switch
        {
            "client" => "client",
            "provider" => "provider",
            "admin" => "admin",
            _ => "unknown"
        };
    }

    private static string NormalizeSnippet(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 120
            ? trimmed
            : $"{trimmed[..120]}...";
    }
}
