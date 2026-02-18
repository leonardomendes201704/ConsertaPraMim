using System.Globalization;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderApiProviderCreditService : IProviderCreditService
{
    private readonly ProviderApiCaller _apiCaller;

    public ProviderApiProviderCreditService(ProviderApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public async Task<ProviderCreditBalanceDto> GetBalanceAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        var response = await _apiCaller.SendAsync<ProviderCreditBalanceDto>(
            HttpMethod.Get,
            "/api/provider-credits/me/balance",
            cancellationToken: cancellationToken);

        return response.Payload ?? new ProviderCreditBalanceDto(providerId, 0m, DateTime.UtcNow);
    }

    public async Task<ProviderCreditStatementDto> GetStatementAsync(
        Guid providerId,
        ProviderCreditStatementQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var parameters = new List<string>();
        if (query.FromUtc.HasValue)
        {
            parameters.Add($"fromUtc={Uri.EscapeDataString(query.FromUtc.Value.ToString("O", CultureInfo.InvariantCulture))}");
        }

        if (query.ToUtc.HasValue)
        {
            parameters.Add($"toUtc={Uri.EscapeDataString(query.ToUtc.Value.ToString("O", CultureInfo.InvariantCulture))}");
        }

        if (query.EntryType.HasValue)
        {
            parameters.Add($"entryType={Uri.EscapeDataString(query.EntryType.Value.ToString())}");
        }

        parameters.Add($"page={query.Page}");
        parameters.Add($"pageSize={query.PageSize}");

        var path = "/api/provider-credits/me/statement?" + string.Join("&", parameters);
        var response = await _apiCaller.SendAsync<ProviderCreditStatementDto>(
            HttpMethod.Get,
            path,
            cancellationToken: cancellationToken);

        return response.Payload ?? new ProviderCreditStatementDto(providerId, 0m, null, query.Page, query.PageSize, 0, []);
    }

    public Task<ProviderCreditMutationResultDto> ApplyMutationAsync(
        ProviderCreditMutationRequestDto request,
        Guid? actorUserId,
        string? actorEmail,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderCreditMutationResultDto(
            false,
            null,
            null,
            "not_supported",
            "Operacao nao suportada no portal prestador."));
    }
}
