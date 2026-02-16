using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

/// <summary>
/// Payload de concessao administrativa de credito para prestador.
/// </summary>
/// <param name="ProviderId">Identificador do prestador que recebera o credito.</param>
/// <param name="Amount">Valor em reais a ser concedido (maior que zero).</param>
/// <param name="Reason">Motivo operacional/comercial da concessao.</param>
/// <param name="GrantType">Tipo da concessao (`Premio`, `Campanha`, `Ajuste`).</param>
/// <param name="ExpiresAtUtc">Data limite de uso do credito em UTC (obrigatoria para campanha, opcional nos demais).</param>
/// <param name="Notes">Observacoes operacionais adicionais (opcional).</param>
/// <param name="CampaignCode">Codigo da campanha quando aplicavel (opcional).</param>
public record AdminProviderCreditGrantRequestDto(
    Guid ProviderId,
    decimal Amount,
    string Reason,
    ProviderCreditGrantType GrantType,
    DateTime? ExpiresAtUtc = null,
    string? Notes = null,
    string? CampaignCode = null);

/// <summary>
/// Payload de estorno/ajuste administrativo para remocao de credito nao consumido.
/// </summary>
/// <param name="ProviderId">Identificador do prestador alvo do estorno.</param>
/// <param name="Amount">Valor em reais a estornar do saldo atual.</param>
/// <param name="Reason">Motivo do estorno/ajuste.</param>
/// <param name="OriginalEntryId">Lancamento original de referencia (opcional).</param>
/// <param name="Notes">Observacoes operacionais adicionais (opcional).</param>
public record AdminProviderCreditReversalRequestDto(
    Guid ProviderId,
    decimal Amount,
    string Reason,
    Guid? OriginalEntryId = null,
    string? Notes = null);

/// <summary>
/// Retorno padrao dos fluxos administrativos de credito.
/// </summary>
/// <param name="Success">Indica se a operacao concluiu com sucesso.</param>
/// <param name="CreditMutation">Resultado detalhado da mutacao no ledger.</param>
/// <param name="NotificationSent">Indica se notificacao ao prestador foi enviada.</param>
/// <param name="NotificationSubject">Assunto utilizado na notificacao enviada.</param>
/// <param name="ErrorCode">Codigo de erro quando `Success=false`.</param>
/// <param name="ErrorMessage">Mensagem de erro de negocio quando `Success=false`.</param>
public record AdminProviderCreditMutationResultDto(
    bool Success,
    ProviderCreditMutationResultDto? CreditMutation = null,
    bool NotificationSent = false,
    string? NotificationSubject = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

