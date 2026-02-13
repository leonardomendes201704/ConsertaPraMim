using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.Application.Services;

public class ProviderCreditService : IProviderCreditService
{
    private readonly IProviderCreditRepository _providerCreditRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;
    private readonly ILogger<ProviderCreditService> _logger;

    public ProviderCreditService(
        IProviderCreditRepository providerCreditRepository,
        IUserRepository userRepository,
        IAdminAuditLogRepository adminAuditLogRepository,
        ILogger<ProviderCreditService>? logger = null)
    {
        _providerCreditRepository = providerCreditRepository;
        _userRepository = userRepository;
        _adminAuditLogRepository = adminAuditLogRepository;
        _logger = logger ?? NullLogger<ProviderCreditService>.Instance;
    }

    public async Task<ProviderCreditBalanceDto> GetBalanceAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        await EnsureProviderUserAsync(providerId, cancellationToken);

        var wallet = await _providerCreditRepository.EnsureWalletAsync(providerId, cancellationToken);
        return new ProviderCreditBalanceDto(wallet.ProviderId, wallet.CurrentBalance, wallet.LastMovementAtUtc);
    }

    public async Task<ProviderCreditStatementDto> GetStatementAsync(
        Guid providerId,
        ProviderCreditStatementQueryDto query,
        CancellationToken cancellationToken = default)
    {
        await EnsureProviderUserAsync(providerId, cancellationToken);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        var fromUtc = query.FromUtc?.ToUniversalTime();
        var toUtc = query.ToUtc?.ToUniversalTime();

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            throw new InvalidOperationException("Periodo invalido para consulta de extrato.");
        }

        var wallet = await _providerCreditRepository.EnsureWalletAsync(providerId, cancellationToken);
        var (items, totalCount) = await _providerCreditRepository.GetStatementAsync(
            providerId,
            fromUtc,
            toUtc,
            query.EntryType,
            page,
            pageSize,
            cancellationToken);

        return new ProviderCreditStatementDto(
            providerId,
            wallet.CurrentBalance,
            wallet.LastMovementAtUtc,
            page,
            pageSize,
            totalCount,
            items.Select(MapItem).ToList());
    }

    public async Task<ProviderCreditMutationResultDto> ApplyMutationAsync(
        ProviderCreditMutationRequestDto request,
        Guid? actorUserId,
        string? actorEmail,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureProviderUserAsync(request.ProviderId, cancellationToken);
            ValidateMutationRequest(request);

            var normalizedAmount = NormalizeAmount(request.Amount);
            var effectiveAtUtc = (request.EffectiveAtUtc ?? DateTime.UtcNow).ToUniversalTime();
            var expiresAtUtc = request.ExpiresAtUtc?.ToUniversalTime();
            if (expiresAtUtc.HasValue && expiresAtUtc.Value <= effectiveAtUtc)
            {
                return Fail("invalid_expiration", "A expiracao deve ser maior que a data efetiva.");
            }

            ProviderCreditLedgerEntry? created = null;
            created = await _providerCreditRepository.AppendEntryAsync(
                request.ProviderId,
                wallet =>
                {
                    var balanceBefore = wallet.CurrentBalance;
                    var balanceAfter = request.EntryType switch
                    {
                        ProviderCreditLedgerEntryType.Grant => balanceBefore + normalizedAmount,
                        ProviderCreditLedgerEntryType.Reversal => balanceBefore + normalizedAmount,
                        ProviderCreditLedgerEntryType.Debit => balanceBefore - normalizedAmount,
                        ProviderCreditLedgerEntryType.Expire => balanceBefore - normalizedAmount,
                        _ => balanceBefore
                    };

                    if (balanceAfter < 0)
                    {
                        throw new CreditValidationException("insufficient_balance", "Saldo insuficiente para consumo/expiracao.");
                    }

                    return new ProviderCreditLedgerEntry
                    {
                        ProviderId = request.ProviderId,
                        EntryType = request.EntryType,
                        Amount = normalizedAmount,
                        BalanceBefore = balanceBefore,
                        BalanceAfter = balanceAfter,
                        Reason = request.Reason.Trim(),
                        Source = NormalizeNullable(request.Source),
                        ReferenceType = NormalizeNullable(request.ReferenceType),
                        ReferenceId = request.ReferenceId,
                        EffectiveAtUtc = effectiveAtUtc,
                        ExpiresAtUtc = expiresAtUtc,
                        AdminUserId = actorUserId,
                        AdminEmail = NormalizeNullable(actorEmail),
                        Metadata = NormalizeNullable(request.Metadata)
                    };
                },
                cancellationToken);

            var currentBalance = new ProviderCreditBalanceDto(
                request.ProviderId,
                created.BalanceAfter,
                created.EffectiveAtUtc);

            await WriteAuditAsync(actorUserId, actorEmail, request, created);
            _logger.LogInformation(
                "Provider credit mutation applied. ProviderId={ProviderId}, EntryType={EntryType}, Amount={Amount}, BalanceAfter={BalanceAfter}",
                request.ProviderId,
                request.EntryType,
                normalizedAmount,
                created.BalanceAfter);

            return new ProviderCreditMutationResultDto(true, currentBalance, MapItem(created));
        }
        catch (CreditValidationException ex)
        {
            _logger.LogWarning(
                "Provider credit mutation validation failed. ProviderId={ProviderId}, ErrorCode={ErrorCode}",
                request.ProviderId,
                ex.ErrorCode);
            return Fail(ex.ErrorCode, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Provider credit mutation failed with invalid operation. ProviderId={ProviderId}",
                request.ProviderId);
            return Fail("validation_error", ex.Message);
        }
    }

    private async Task EnsureProviderUserAsync(Guid providerId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(providerId);
        if (user == null || user.Role != UserRole.Provider)
        {
            throw new InvalidOperationException("Prestador nao encontrado para operacao de credito.");
        }
    }

    private static void ValidateMutationRequest(ProviderCreditMutationRequestDto request)
    {
        if (request.ProviderId == Guid.Empty)
        {
            throw new CreditValidationException("provider_invalid", "Prestador invalido.");
        }

        if (request.Amount <= 0)
        {
            throw new CreditValidationException("amount_invalid", "Valor deve ser maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new CreditValidationException("reason_required", "Motivo obrigatorio.");
        }

        if (request.Reason.Trim().Length > 500)
        {
            throw new CreditValidationException("reason_too_long", "Motivo deve ter no maximo 500 caracteres.");
        }
    }

    private async Task WriteAuditAsync(
        Guid? actorUserId,
        string? actorEmail,
        ProviderCreditMutationRequestDto request,
        ProviderCreditLedgerEntry entry)
    {
        var resolvedActorUserId = actorUserId ?? Guid.Empty;
        var resolvedActorEmail = string.IsNullOrWhiteSpace(actorEmail) ? "system@consertapramim.local" : actorEmail.Trim();

        var metadata = JsonSerializer.Serialize(new
        {
            providerId = request.ProviderId,
            entryType = entry.EntryType.ToString(),
            amount = entry.Amount,
            balanceBefore = entry.BalanceBefore,
            balanceAfter = entry.BalanceAfter,
            reason = entry.Reason,
            source = entry.Source,
            referenceType = entry.ReferenceType,
            referenceId = entry.ReferenceId,
            effectiveAtUtc = entry.EffectiveAtUtc,
            expiresAtUtc = entry.ExpiresAtUtc
        });

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = resolvedActorUserId,
            ActorEmail = resolvedActorEmail,
            Action = BuildAuditAction(entry.EntryType),
            TargetType = "ProviderCreditWallet",
            TargetId = entry.WalletId,
            Metadata = metadata
        });
    }

    private static string BuildAuditAction(ProviderCreditLedgerEntryType entryType)
    {
        return entryType switch
        {
            ProviderCreditLedgerEntryType.Grant => "ProviderCreditGrantCreated",
            ProviderCreditLedgerEntryType.Debit => "ProviderCreditDebitCreated",
            ProviderCreditLedgerEntryType.Expire => "ProviderCreditExpireCreated",
            ProviderCreditLedgerEntryType.Reversal => "ProviderCreditReversalCreated",
            _ => "ProviderCreditEntryCreated"
        };
    }

    private static ProviderCreditStatementItemDto MapItem(ProviderCreditLedgerEntry entry)
    {
        return new ProviderCreditStatementItemDto(
            entry.Id,
            entry.EntryType,
            entry.Amount,
            entry.BalanceBefore,
            entry.BalanceAfter,
            entry.Reason,
            entry.Source,
            entry.ReferenceType,
            entry.ReferenceId,
            entry.EffectiveAtUtc,
            entry.ExpiresAtUtc,
            entry.AdminUserId,
            entry.AdminEmail,
            entry.CreatedAt);
    }

    private static decimal NormalizeAmount(decimal amount)
    {
        return decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ProviderCreditMutationResultDto Fail(string errorCode, string errorMessage)
    {
        return new ProviderCreditMutationResultDto(false, null, null, errorCode, errorMessage);
    }

    private sealed class CreditValidationException : Exception
    {
        public CreditValidationException(string errorCode, string message)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public string ErrorCode { get; }
    }
}
