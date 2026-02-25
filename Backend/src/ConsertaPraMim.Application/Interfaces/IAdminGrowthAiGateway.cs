namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminGrowthAiGateway
{
    Task<AdminGrowthAiGatewayResult> GenerateAnalysisAsync(
        AdminGrowthAiGatewayRequest request,
        CancellationToken cancellationToken = default);
}

public record AdminGrowthAiGatewayRequest(
    string ApiKey,
    string Model,
    decimal Temperature,
    int MaxOutputTokens,
    string SystemPrompt,
    string UserPrompt);

public record AdminGrowthAiGatewayResult(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    string? OutputText = null,
    int? InputTokens = null,
    int? OutputTokens = null,
    int? TotalTokens = null);
