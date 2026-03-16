using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramJourneySchedulingClient : ITelegramJourneySchedulingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TelegramAutomationOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TelegramJourneySchedulingClient> _logger;

    public TelegramJourneySchedulingClient(
        HttpClient httpClient,
        IOptions<TelegramAutomationOptions> options,
        IHttpContextAccessor httpContextAccessor,
        ILogger<TelegramJourneySchedulingClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<TelegramJourneySchedulingTurnResult> ProcessTurnAsync(
        TelegramJourneySchedulingTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled || !_options.ClientsAutomationEnabled)
        {
            return TelegramJourneySchedulingTurnResult.Disabled("Autoagendamento da jornada desabilitado no ambiente atual.");
        }

        if (_httpClient.BaseAddress is null)
        {
            return TelegramJourneySchedulingTurnResult.Failed(
                StatusCodes.Status503ServiceUnavailable,
                "URL do CPM Full nao configurada para autoagendamento da jornada.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/telegram/automation/scheduling/turn")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.TryAddWithoutValidation("Accept", "application/json");
        message.Headers.TryAddWithoutValidation("User-Agent", "ConsertaPraMim.Web.TelegramBridge/1.0");
        message.Headers.TryAddWithoutValidation("X-Telegram-Automation-Key", _options.SharedSecret);
        message.Headers.TryAddWithoutValidation("X-Correlation-ID", ResolveCorrelationId(request.ChatbotConversationId));

        try
        {
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<TelegramJourneySchedulingApiResponse>(JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var failureMessage = payload?.Message;
                if (string.IsNullOrWhiteSpace(failureMessage))
                {
                    failureMessage = "Falha ao processar autoagendamento da jornada no CPM Full.";
                }

                _logger.LogWarning(
                    "Autoagendamento Telegram retornou erro HTTP {StatusCode} para a conversa {ChatbotConversationId}. Message={Message}",
                    (int)response.StatusCode,
                    request.ChatbotConversationId,
                    TelegramSecuritySanitizer.SanitizeMessage(failureMessage, 300));

                return TelegramJourneySchedulingTurnResult.Failed((int)response.StatusCode, failureMessage);
            }

            return new TelegramJourneySchedulingTurnResult
            {
                Success = payload?.Success ?? true,
                Handled = payload?.Handled ?? false,
                HttpStatusCode = (int)response.StatusCode,
                LeadId = payload?.LeadId ?? 0,
                JourneyId = payload?.JourneyId ?? 0,
                CurrentState = payload?.CurrentState ?? string.Empty,
                SchedulingStatus = payload?.SchedulingStatus ?? string.Empty,
                Message = payload?.Message ?? "Turno de autoagendamento processado.",
                ReplyText = payload?.ReplyText ?? string.Empty,
                RemoveReplyKeyboard = payload?.RemoveReplyKeyboard ?? false,
                GoogleCalendarEventId = payload?.GoogleCalendarEventId ?? string.Empty,
                GoogleCalendarEventLink = payload?.GoogleCalendarEventLink ?? string.Empty,
                ScheduledStartAtUtc = payload?.ScheduledStartAtUtc,
                ScheduledEndAtUtc = payload?.ScheduledEndAtUtc,
                SuggestedSlots = payload?.SuggestedSlots?
                    .Select(item => new TelegramJourneySchedulingSuggestedSlot
                    {
                        OptionNumber = item.OptionNumber,
                        StartsAtUtc = item.StartsAtUtc,
                        EndsAtUtc = item.EndsAtUtc,
                        Label = item.Label
                    })
                    .ToList() ?? []
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Falha ao chamar autoagendamento da jornada no CPM Full para a conversa {ChatbotConversationId}.",
                request.ChatbotConversationId);
            return TelegramJourneySchedulingTurnResult.Failed(
                StatusCodes.Status502BadGateway,
                "Falha ao comunicar com o CPM Full para autoagendamento da jornada.");
        }
    }

    private string ResolveCorrelationId(Guid chatbotConversationId)
    {
        var headerValue = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-ID"].ToString();
        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.Trim();
        }

        return $"telegram-scheduling-{chatbotConversationId:N}";
    }

    private sealed class TelegramJourneySchedulingApiResponse
    {
        public bool Success { get; init; }
        public bool Handled { get; init; }
        public int LeadId { get; init; }
        public int JourneyId { get; init; }
        public string CurrentState { get; init; } = string.Empty;
        public string SchedulingStatus { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string ReplyText { get; init; } = string.Empty;
        public bool RemoveReplyKeyboard { get; init; }
        public string GoogleCalendarEventId { get; init; } = string.Empty;
        public string GoogleCalendarEventLink { get; init; } = string.Empty;
        public DateTime? ScheduledStartAtUtc { get; init; }
        public DateTime? ScheduledEndAtUtc { get; init; }
        public IReadOnlyList<TelegramJourneySchedulingApiSuggestedSlot>? SuggestedSlots { get; init; }
    }

    private sealed class TelegramJourneySchedulingApiSuggestedSlot
    {
        public int OptionNumber { get; init; }
        public DateTime StartsAtUtc { get; init; }
        public DateTime EndsAtUtc { get; init; }
        public string Label { get; init; } = string.Empty;
    }
}
