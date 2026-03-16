using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.API.Integrations.Journey;

public sealed class JourneyAutomationGateway : IServiceJourneyAutomationGateway
{
    private const string SharedSecretHeaderName = "X-Journey-Automation-Key";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly JourneyAutomationGatewayOptions _options;
    private readonly ILogger<JourneyAutomationGateway> _logger;

    public JourneyAutomationGateway(
        HttpClient httpClient,
        IOptions<JourneyAutomationGatewayOptions> options,
        ILogger<JourneyAutomationGateway> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ServiceJourneyAutomationResultDto> UpsertJourneyAsync(
        ServiceJourneyAutomationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled)
        {
            return ServiceJourneyAutomationResultDto.Disabled("Automacao de jornada desabilitada no ambiente atual.");
        }

        var normalizedBoardType = string.IsNullOrWhiteSpace(request.BoardType)
            ? string.Empty
            : request.BoardType.Trim().ToLowerInvariant();

        if (normalizedBoardType == "clientes" && !_options.ClientsAutomationEnabled)
        {
            return ServiceJourneyAutomationResultDto.Disabled("Automacao de jornada para clientes desabilitada no ambiente atual.");
        }

        if (normalizedBoardType == "prestadores" && !_options.ProvidersAutomationEnabled)
        {
            return ServiceJourneyAutomationResultDto.Disabled("Automacao de jornada para prestadores desabilitada no ambiente atual.");
        }

        if (_httpClient.BaseAddress is null)
        {
            return ServiceJourneyAutomationResultDto.Failed(StatusCodes.Status503ServiceUnavailable, "URL do CPM Full nao configurada para automacao da jornada.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/journey/automation/intake")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.TryAddWithoutValidation("Accept", "application/json");
        message.Headers.TryAddWithoutValidation("User-Agent", "ConsertaPraMim.API/1.0");
        message.Headers.TryAddWithoutValidation(SharedSecretHeaderName, _options.SharedSecret);
        message.Headers.TryAddWithoutValidation("X-Correlation-ID", ResolveCorrelationId(request));

        try
        {
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<JourneyAutomationApiResponse>(JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var failureMessage = string.IsNullOrWhiteSpace(payload?.Message)
                    ? "Falha ao sincronizar a jornada com o CPM Full."
                    : payload!.Message;

                _logger.LogWarning(
                    "Automacao da jornada retornou erro HTTP {StatusCode} para BoardType={BoardType}. Message={Message}",
                    (int)response.StatusCode,
                    request.BoardType,
                    failureMessage);

                return ServiceJourneyAutomationResultDto.Failed((int)response.StatusCode, failureMessage);
            }

            return new ServiceJourneyAutomationResultDto
            {
                Success = payload?.Success ?? true,
                HttpStatusCode = (int)response.StatusCode,
                Message = payload?.Message ?? "Jornada sincronizada com sucesso.",
                LeadId = payload?.LeadId,
                JourneyId = payload?.JourneyId,
                JourneyPublicId = payload?.JourneyPublicId,
                CreatedLead = payload?.CreatedLead ?? false,
                CreatedJourney = payload?.CreatedJourney ?? false,
                BoardType = payload?.BoardType ?? request.BoardType,
                CurrentState = payload?.CurrentState ?? string.Empty
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao chamar a automacao da jornada no CPM Full para BoardType={BoardType}.", request.BoardType);
            return ServiceJourneyAutomationResultDto.Failed(StatusCodes.Status502BadGateway, "Falha ao comunicar com o CPM Full para automacao da jornada.");
        }
    }

    private static string ResolveCorrelationId(ServiceJourneyAutomationRequestDto request)
    {
        if (request.ServiceRequestId.HasValue)
        {
            return $"journey-service-request-{request.ServiceRequestId.Value:N}";
        }

        if (request.LandingLeadId.HasValue)
        {
            return $"journey-landing-{request.LandingLeadId.Value:N}";
        }

        if (request.ChatbotConversationId.HasValue)
        {
            return $"journey-telegram-{request.ChatbotConversationId.Value:N}";
        }

        return $"journey-{Guid.NewGuid():N}";
    }

    private sealed class JourneyAutomationApiResponse
    {
        public bool Success { get; init; }
        public int LeadId { get; init; }
        public int JourneyId { get; init; }
        public Guid JourneyPublicId { get; init; }
        public bool CreatedLead { get; init; }
        public bool CreatedJourney { get; init; }
        public string BoardType { get; init; } = string.Empty;
        public string CurrentState { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }
}
