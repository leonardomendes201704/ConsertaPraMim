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

/// <summary>
/// Filtros administrativos para relatório consolidado de créditos por prestador.
/// </summary>
/// <param name="FromUtc">Data inicial UTC (opcional).</param>
/// <param name="ToUtc">Data final UTC (opcional).</param>
/// <param name="EntryType">Tipo de movimentação (`Grant`, `Debit`, `Expire`, `Reversal`) opcional.</param>
/// <param name="Status">Status lógico do movimento (`all`, `credit`, `debit`).</param>
/// <param name="SearchTerm">Busca textual por nome/email do prestador (opcional).</param>
/// <param name="Page">Página atual (mínimo 1).</param>
/// <param name="PageSize">Itens por página (mínimo 1, máximo 100).</param>
public record AdminProviderCreditUsageReportQueryDto(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    ProviderCreditLedgerEntryType? EntryType = null,
    string Status = "all",
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 20);

/// <summary>
/// Linha do relatório consolidado de crédito por prestador.
/// </summary>
/// <param name="ProviderId">Identificador do prestador.</param>
/// <param name="ProviderName">Nome do prestador.</param>
/// <param name="ProviderEmail">Email do prestador.</param>
/// <param name="CurrentBalance">Saldo atual da carteira.</param>
/// <param name="GrantedAmount">Créditos concedidos no recorte.</param>
/// <param name="ConsumedAmount">Créditos consumidos no recorte.</param>
/// <param name="ExpiredAmount">Créditos expirados no recorte.</param>
/// <param name="ReversedAmount">Créditos estornados no recorte.</param>
/// <param name="NetVariation">Variação líquida no período.</param>
/// <param name="MovementCount">Quantidade de movimentos no recorte filtrado.</param>
/// <param name="LastMovementAtUtc">Data/hora UTC do último movimento do prestador.</param>
public record AdminProviderCreditUsageReportItemDto(
    Guid ProviderId,
    string ProviderName,
    string ProviderEmail,
    decimal CurrentBalance,
    decimal GrantedAmount,
    decimal ConsumedAmount,
    decimal ExpiredAmount,
    decimal ReversedAmount,
    decimal NetVariation,
    int MovementCount,
    DateTime? LastMovementAtUtc);

/// <summary>
/// Resultado paginado do relatório administrativo de uso de créditos.
/// </summary>
/// <param name="FromUtc">Data inicial efetiva aplicada.</param>
/// <param name="ToUtc">Data final efetiva aplicada.</param>
/// <param name="Status">Status lógico aplicado (`all`, `credit`, `debit`).</param>
/// <param name="EntryType">Tipo de movimentação aplicado.</param>
/// <param name="SearchTerm">Busca aplicada.</param>
/// <param name="Page">Página atual.</param>
/// <param name="PageSize">Itens por página.</param>
/// <param name="TotalProviders">Quantidade total de prestadores elegíveis no relatório.</param>
/// <param name="TotalGranted">Soma de créditos concedidos no recorte.</param>
/// <param name="TotalConsumed">Soma de créditos consumidos no recorte.</param>
/// <param name="TotalExpired">Soma de créditos expirados no recorte.</param>
/// <param name="TotalReversed">Soma de créditos estornados no recorte.</param>
/// <param name="TotalOpenBalance">Saldo total aberto das carteiras listadas.</param>
/// <param name="Items">Itens paginados do relatório.</param>
public record AdminProviderCreditUsageReportDto(
    DateTime FromUtc,
    DateTime ToUtc,
    string Status,
    ProviderCreditLedgerEntryType? EntryType,
    string? SearchTerm,
    int Page,
    int PageSize,
    int TotalProviders,
    decimal TotalGranted,
    decimal TotalConsumed,
    decimal TotalExpired,
    decimal TotalReversed,
    decimal TotalOpenBalance,
    IReadOnlyList<AdminProviderCreditUsageReportItemDto> Items);
