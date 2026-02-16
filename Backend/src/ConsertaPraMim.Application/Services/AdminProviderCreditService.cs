using System.Globalization;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.Application.Services;

public class AdminProviderCreditService : IAdminProviderCreditService
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private readonly IUserRepository _userRepository;
    private readonly IProviderCreditService _providerCreditService;
    private readonly INotificationService _notificationService;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;
    private readonly ILogger<AdminProviderCreditService> _logger;

    public AdminProviderCreditService(
        IUserRepository userRepository,
        IProviderCreditService providerCreditService,
        INotificationService notificationService,
        IAdminAuditLogRepository adminAuditLogRepository,
        ILogger<AdminProviderCreditService>? logger = null)
    {
        _userRepository = userRepository;
        _providerCreditService = providerCreditService;
        _notificationService = notificationService;
        _adminAuditLogRepository = adminAuditLogRepository;
        _logger = logger ?? NullLogger<AdminProviderCreditService>.Instance;
    }

    public async Task<AdminProviderCreditMutationResultDto> GrantAsync(
        AdminProviderCreditGrantRequestDto request,
        Guid actorUserId,
        string actorEmail,
        CancellationToken cancellationToken = default)
    {
        var provider = await ResolveProviderAsync(request.ProviderId);
        if (provider == null)
        {
            return Fail("not_found", "Prestador nao encontrado para concessao de credito.");
        }

        if (!provider.IsActive)
        {
            return Fail("provider_inactive", "Prestador inativo nao pode receber credito administrativo.");
        }

        if (request.Amount <= 0)
        {
            return Fail("invalid_payload", "Valor deve ser maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Fail("invalid_payload", "Motivo da concessao e obrigatorio.");
        }

        var nowUtc = DateTime.UtcNow;
        if (!TryResolveGrantExpiration(request, nowUtc, out var expiresAtUtc, out var expirationError))
        {
            return Fail("invalid_payload", expirationError ?? "Regra de expiracao invalida.");
        }

        var balanceBefore = await _providerCreditService.GetBalanceAsync(request.ProviderId, cancellationToken);
        var normalizedReason = BuildReason(request.Reason, request.Notes);
        var mutation = await _providerCreditService.ApplyMutationAsync(
            new ProviderCreditMutationRequestDto(
                request.ProviderId,
                ProviderCreditLedgerEntryType.Grant,
                request.Amount,
                normalizedReason,
                Source: $"AdminPortal.{request.GrantType}",
                ReferenceType: "AdminCreditGrant",
                ReferenceId: null,
                EffectiveAtUtc: nowUtc,
                ExpiresAtUtc: expiresAtUtc,
                Metadata: JsonSerializer.Serialize(new
                {
                    grantType = request.GrantType.ToString(),
                    notes = NormalizeNullable(request.Notes),
                    campaignCode = NormalizeNullable(request.CampaignCode)
                })),
            actorUserId,
            actorEmail,
            cancellationToken);

        if (!mutation.Success)
        {
            return Fail(mutation.ErrorCode ?? "credit_mutation_error", mutation.ErrorMessage ?? "Falha ao conceder credito.");
        }

        var (subject, message) = BuildGrantNotification(request, mutation, expiresAtUtc);
        var notificationSent = await TrySendNotificationAsync(provider, subject, message);

        await WriteAdminAuditAsync(
            actorUserId,
            actorEmail,
            "AdminProviderCreditGrantExecuted",
            request.ProviderId,
            balanceBefore.CurrentBalance,
            mutation.Balance?.CurrentBalance,
            new
            {
                operation = "grant",
                grantType = request.GrantType.ToString(),
                reason = normalizedReason,
                request.Amount,
                expiresAtUtc,
                request.CampaignCode,
                notificationSent,
                notificationSubject = subject,
                entryId = mutation.Entry?.EntryId
            });

        _logger.LogInformation(
            "Admin credit grant executed. ProviderId={ProviderId}, GrantType={GrantType}, Amount={Amount}, NotificationSent={NotificationSent}",
            request.ProviderId,
            request.GrantType,
            request.Amount,
            notificationSent);

        return new AdminProviderCreditMutationResultDto(
            true,
            mutation,
            notificationSent,
            subject);
    }

    public async Task<AdminProviderCreditMutationResultDto> ReverseAsync(
        AdminProviderCreditReversalRequestDto request,
        Guid actorUserId,
        string actorEmail,
        CancellationToken cancellationToken = default)
    {
        var provider = await ResolveProviderAsync(request.ProviderId);
        if (provider == null)
        {
            return Fail("not_found", "Prestador nao encontrado para estorno de credito.");
        }

        if (request.Amount <= 0)
        {
            return Fail("invalid_payload", "Valor de estorno deve ser maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Fail("invalid_payload", "Motivo do estorno e obrigatorio.");
        }

        var nowUtc = DateTime.UtcNow;
        var balanceBefore = await _providerCreditService.GetBalanceAsync(request.ProviderId, cancellationToken);
        var normalizedReason = BuildReason(request.Reason, request.Notes);
        var mutation = await _providerCreditService.ApplyMutationAsync(
            new ProviderCreditMutationRequestDto(
                request.ProviderId,
                ProviderCreditLedgerEntryType.Debit,
                request.Amount,
                normalizedReason,
                Source: "AdminPortal.Reversal",
                ReferenceType: "AdminCreditReversal",
                ReferenceId: request.OriginalEntryId,
                EffectiveAtUtc: nowUtc,
                ExpiresAtUtc: null,
                Metadata: JsonSerializer.Serialize(new
                {
                    originalEntryId = request.OriginalEntryId,
                    notes = NormalizeNullable(request.Notes)
                })),
            actorUserId,
            actorEmail,
            cancellationToken);

        if (!mutation.Success)
        {
            return Fail(mutation.ErrorCode ?? "credit_mutation_error", mutation.ErrorMessage ?? "Falha ao estornar credito.");
        }

        var subject = "Ajuste de creditos na sua carteira";
        var message = BuildReversalNotification(request, mutation);
        var notificationSent = await TrySendNotificationAsync(provider, subject, message);

        await WriteAdminAuditAsync(
            actorUserId,
            actorEmail,
            "AdminProviderCreditReversalExecuted",
            request.ProviderId,
            balanceBefore.CurrentBalance,
            mutation.Balance?.CurrentBalance,
            new
            {
                operation = "reversal",
                reason = normalizedReason,
                request.Amount,
                request.OriginalEntryId,
                notificationSent,
                notificationSubject = subject,
                entryId = mutation.Entry?.EntryId
            });

        _logger.LogInformation(
            "Admin credit reversal executed. ProviderId={ProviderId}, Amount={Amount}, NotificationSent={NotificationSent}",
            request.ProviderId,
            request.Amount,
            notificationSent);

        return new AdminProviderCreditMutationResultDto(
            true,
            mutation,
            notificationSent,
            subject);
    }

    private async Task<User?> ResolveProviderAsync(Guid providerId)
    {
        if (providerId == Guid.Empty)
        {
            return null;
        }

        var user = await _userRepository.GetByIdAsync(providerId);
        if (user?.Role != UserRole.Provider)
        {
            return null;
        }

        return user;
    }

    private static bool TryResolveGrantExpiration(
        AdminProviderCreditGrantRequestDto request,
        DateTime nowUtc,
        out DateTime? expiresAtUtc,
        out string? error)
    {
        expiresAtUtc = request.ExpiresAtUtc?.ToUniversalTime();
        error = null;

        switch (request.GrantType)
        {
            case ProviderCreditGrantType.Campanha:
                if (!expiresAtUtc.HasValue)
                {
                    error = "Creditos de campanha exigem data limite de uso.";
                    return false;
                }

                if (expiresAtUtc.Value <= nowUtc.AddMinutes(1))
                {
                    error = "Data limite da campanha deve ser futura.";
                    return false;
                }

                if (expiresAtUtc.Value > nowUtc.AddDays(365))
                {
                    error = "Data limite da campanha nao pode exceder 365 dias.";
                    return false;
                }

                return true;

            case ProviderCreditGrantType.Premio:
                if (!expiresAtUtc.HasValue)
                {
                    expiresAtUtc = nowUtc.AddDays(90);
                    return true;
                }

                if (expiresAtUtc.Value <= nowUtc.AddMinutes(1))
                {
                    error = "Data limite do premio deve ser futura.";
                    return false;
                }

                return true;

            case ProviderCreditGrantType.Ajuste:
                if (expiresAtUtc.HasValue && expiresAtUtc.Value <= nowUtc.AddMinutes(1))
                {
                    error = "Data limite do ajuste deve ser futura.";
                    return false;
                }

                return true;

            default:
                error = "Tipo de concessao invalido.";
                return false;
        }
    }

    private static (string Subject, string Message) BuildGrantNotification(
        AdminProviderCreditGrantRequestDto request,
        ProviderCreditMutationResultDto mutation,
        DateTime? expiresAtUtc)
    {
        var amount = mutation.Entry?.Amount ?? request.Amount;
        var amountText = amount.ToString("C", PtBr);
        var expirationText = expiresAtUtc.HasValue
            ? $"Validade: {expiresAtUtc.Value.ToLocalTime():dd/MM/yyyy HH:mm}."
            : "Sem data de expiracao definida.";

        return request.GrantType switch
        {
            ProviderCreditGrantType.Premio => (
                "Voce recebeu um premio em creditos",
                $"Parabens! Foram creditados {amountText} na sua carteira. Motivo: {request.Reason.Trim()}. {expirationText}"),
            ProviderCreditGrantType.Campanha => (
                "Credito de campanha concedido",
                $"Voce recebeu {amountText} referente a campanha administrativa. Motivo: {request.Reason.Trim()}. {expirationText}"),
            _ => (
                "Ajuste de credito realizado",
                $"Um ajuste de {amountText} foi aplicado na sua carteira. Motivo: {request.Reason.Trim()}. {expirationText}")
        };
    }

    private static string BuildReversalNotification(
        AdminProviderCreditReversalRequestDto request,
        ProviderCreditMutationResultDto mutation)
    {
        var amount = mutation.Entry?.Amount ?? request.Amount;
        var amountText = amount.ToString("C", PtBr);
        var referenceText = request.OriginalEntryId.HasValue
            ? $" Referencia: {request.OriginalEntryId.Value}."
            : string.Empty;

        return $"Foi aplicado um estorno de {amountText} na sua carteira de creditos. Motivo: {request.Reason.Trim()}.{referenceText}";
    }

    private async Task<bool> TrySendNotificationAsync(User provider, string subject, string message)
    {
        try
        {
            await _notificationService.SendNotificationAsync(
                provider.Id.ToString("N"),
                subject,
                message,
                "/Profile");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Provider credit notification failed. ProviderId={ProviderId}, Subject={Subject}",
                provider.Id,
                subject);
            return false;
        }
    }

    private async Task WriteAdminAuditAsync(
        Guid actorUserId,
        string actorEmail,
        string action,
        Guid providerId,
        decimal balanceBefore,
        decimal? balanceAfter,
        object details)
    {
        var normalizedActorEmail = string.IsNullOrWhiteSpace(actorEmail)
            ? "admin@consertapramim.local"
            : actorEmail.Trim();

        var metadata = JsonSerializer.Serialize(new
        {
            before = new
            {
                balance = balanceBefore
            },
            after = new
            {
                balance = balanceAfter ?? balanceBefore
            },
            details
        });

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = normalizedActorEmail,
            Action = action,
            TargetType = "ProviderCreditWallet",
            TargetId = providerId,
            Metadata = metadata
        });
    }

    private static string BuildReason(string reason, string? notes)
    {
        var normalizedReason = reason.Trim();
        var normalizedNotes = NormalizeNullable(notes);
        if (normalizedNotes == null)
        {
            return normalizedReason;
        }

        return $"{normalizedReason} | Obs: {normalizedNotes}";
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static AdminProviderCreditMutationResultDto Fail(string errorCode, string errorMessage)
    {
        return new AdminProviderCreditMutationResultDto(
            false,
            null,
            false,
            null,
            errorCode,
            errorMessage);
    }
}

