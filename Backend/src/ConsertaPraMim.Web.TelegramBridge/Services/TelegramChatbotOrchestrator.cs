using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramChatbotOrchestrator : ITelegramChatbotOrchestrator
{
    private const int ConversationStatusActive = 1;
    private const int ActionStatusSucceeded = 2;
    private const int ActionStatusFailed = 3;
    private const int ContextPayloadLimit = 15_000;
    private const int MetadataPayloadLimit = 4_000;
    private const string OpenServiceRequestIntent = "open_service_request";
    private const string ScheduleVisitsIntent = "schedule_visits";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();

    private static readonly Meter Meter = new("ConsertaPraMim.Web.TelegramBridge.AiOrchestrator", "1.0.0");
    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>("telegram_chatbot_ai_requests_total");
    private static readonly Counter<long> FailureCounter = Meter.CreateCounter<long>("telegram_chatbot_ai_failures_total");
    private static readonly Counter<long> FallbackCounter = Meter.CreateCounter<long>("telegram_chatbot_ai_fallback_total");
    private static readonly Histogram<double> LatencyHistogram = Meter.CreateHistogram<double>("telegram_chatbot_ai_latency_ms");
    private static readonly Histogram<long> TokensHistogram = Meter.CreateHistogram<long>("telegram_chatbot_ai_tokens_total");

    private readonly ITelegramAiGateway _telegramAiGateway;
    private readonly ITelegramChatbotApiClient _telegramChatbotApiClient;
    private readonly IOptions<TelegramBridgeAiOptions> _options;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<TelegramChatbotOrchestrator> _logger;
    private readonly TelegramServiceRequestTriageEngine _serviceRequestTriageEngine;
    private readonly TelegramSchedulingNaturalLanguageParser _telegramSchedulingNaturalLanguageParser;

    public TelegramChatbotOrchestrator(
        ITelegramAiGateway telegramAiGateway,
        ITelegramChatbotApiClient telegramChatbotApiClient,
        IOptions<TelegramBridgeAiOptions> options,
        IMemoryCache memoryCache,
        ILogger<TelegramChatbotOrchestrator> logger,
        TelegramServiceRequestTriageEngine serviceRequestTriageEngine,
        TelegramSchedulingNaturalLanguageParser telegramSchedulingNaturalLanguageParser)
    {
        _telegramAiGateway = telegramAiGateway;
        _telegramChatbotApiClient = telegramChatbotApiClient;
        _options = options;
        _memoryCache = memoryCache;
        _logger = logger;
        _serviceRequestTriageEngine = serviceRequestTriageEngine;
        _telegramSchedulingNaturalLanguageParser = telegramSchedulingNaturalLanguageParser;
    }

    public async Task<TelegramChatbotAssistantReply?> GenerateAssistantReplyAsync(
        string apiToken,
        long chatId,
        ChatMessageDto clientMessage,
        string conversationTitle,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            return null;
        }

        var provider = string.IsNullOrWhiteSpace(options.Provider)
            ? "OpenAI"
            : options.Provider.Trim();

        if (!provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Provider de IA nao suportado na bridge. Provider: {Provider}", provider);
            return BuildFallbackReply(options, "provider_not_supported", 0);
        }

        var normalizedTitle = string.IsNullOrWhiteSpace(conversationTitle)
            ? $"Atendimento {chatId}"
            : conversationTitle.Trim();

        var correlationId = CreateCorrelationId();
        var modelName = string.IsNullOrWhiteSpace(options.Model)
            ? "gpt-4.1-mini"
            : options.Model.Trim();

        var conversationId = await _telegramChatbotApiClient.OpenOrResumeSessionAsync(
            apiToken,
            chatId,
            normalizedTitle,
            cancellationToken);

        if (!conversationId.HasValue)
        {
            _logger.LogWarning("Nao foi possivel abrir/retomar conversa para orquestracao IA. ChatId: {ChatId}", chatId);
            return BuildFallbackReply(options, correlationId, 0);
        }

        var cacheKey = BuildCacheKey(
            conversationId.Value,
            clientMessage.Text,
            clientMessage.Attachments.Count,
            options.PromptVersion,
            modelName);

        if (_memoryCache.TryGetValue<TelegramChatbotAssistantReply>(cacheKey, out var cachedReply) &&
            cachedReply is not null)
        {
            var replayed = cachedReply with
            {
                UsedCache = true,
                CorrelationId = correlationId
            };

            RequestCounter.Add(1,
                new KeyValuePair<string, object?>("model", modelName),
                new KeyValuePair<string, object?>("intent", replayed.Intent),
                new KeyValuePair<string, object?>("used_cache", true),
                new KeyValuePair<string, object?>("used_fallback", replayed.UsedFallback));

            return replayed;
        }

        var startedAt = Stopwatch.StartNew();

        TelegramChatbotConversationHistoryDto? history = null;
        TelegramAiGatewayResult gatewayResult;
        TelegramChatbotAssistantReply reply;

        try
        {
            history = await _telegramChatbotApiClient.GetConversationHistoryAsync(
                apiToken,
                conversationId.Value,
                options.MaxContextMessages,
                options.MaxContextSnapshots,
                options.MaxContextActionLogs,
                cancellationToken);

            var promptMessages = BuildPromptMessages(options, history, normalizedTitle, clientMessage);

            gatewayResult = await _telegramAiGateway.GenerateReplyAsync(
                new TelegramAiGatewayRequest(
                    ApiKey: options.ApiKey,
                    Model: modelName,
                    Temperature: options.Temperature,
                    MaxOutputTokens: Math.Clamp(options.MaxOutputTokens, 64, 2048),
                    Messages: promptMessages,
                    RequestTimeoutSeconds: options.RequestTimeoutSeconds,
                    MaxRetries: options.MaxRetries),
                cancellationToken);

            reply = BuildAssistantReplyFromGateway(options, gatewayResult, modelName, correlationId);

            reply = await ApplyServiceRequestTriageAsync(
                apiToken,
                conversationId.Value,
                history,
                clientMessage,
                reply,
                cancellationToken);

            reply = await ApplySchedulingFlowAsync(
                apiToken,
                conversationId.Value,
                history,
                clientMessage,
                reply,
                cancellationToken);

            startedAt.Stop();

            await PersistOrchestrationTrailAsync(
                apiToken,
                conversationId.Value,
                history,
                promptMessages,
                reply,
                gatewayResult,
                options,
                cancellationToken);

            PublishMetrics(modelName, reply, gatewayResult, startedAt.ElapsedMilliseconds);

            if (!reply.UsedFallback)
            {
                var cacheTtlSeconds = Math.Clamp(options.CacheTtlSeconds, 0, 300);
                if (cacheTtlSeconds > 0)
                {
                    _memoryCache.Set(cacheKey, reply, TimeSpan.FromSeconds(cacheTtlSeconds));
                }
            }

            _logger.LogInformation(
                "IA respondeu conversa {ConversationId}. Intent: {Intent}. NextStep: {NextStep}. Fallback: {UsedFallback}. Cache: {UsedCache}. Tokens: {TotalTokens}. LatencyMs: {LatencyMs}. CorrelationId: {CorrelationId}",
                conversationId.Value,
                reply.Intent,
                reply.NextStep,
                reply.UsedFallback,
                reply.UsedCache,
                reply.TotalTokens,
                reply.LatencyMilliseconds,
                correlationId);

            return reply;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            startedAt.Stop();
            _logger.LogError(
                exception,
                "Falha inesperada ao orquestrar resposta IA para ChatId {ChatId}. CorrelationId: {CorrelationId}",
                chatId,
                correlationId);

            FailureCounter.Add(1,
                new KeyValuePair<string, object?>("model", modelName),
                new KeyValuePair<string, object?>("phase", "orchestrator_exception"));

            var fallback = BuildFallbackReply(options, correlationId, startedAt.ElapsedMilliseconds);

            if (conversationId.HasValue)
            {
                await PersistUnexpectedFailureActionAsync(
                    apiToken,
                    conversationId.Value,
                    fallback,
                    exception,
                    cancellationToken);
            }

            return fallback;
        }
    }

    private static TelegramChatbotAssistantReply BuildAssistantReplyFromGateway(
        TelegramBridgeAiOptions options,
        TelegramAiGatewayResult gatewayResult,
        string modelName,
        string correlationId)
    {
        if (!gatewayResult.Success)
        {
            return BuildFallbackReply(options, correlationId, gatewayResult.LatencyMilliseconds);
        }

        var parsed = TelegramAiResponseParser.Parse(gatewayResult.OutputText, options.FallbackMessage);

        return new TelegramChatbotAssistantReply(
            MessageText: parsed.MessageToClient,
            Intent: parsed.Intent,
            NextStep: parsed.NextStep,
            Confidence: parsed.Confidence,
            EntitiesJson: TrimToLimit(parsed.EntitiesJson, MetadataPayloadLimit),
            UsedFallback: false,
            UsedCache: false,
            PromptTokens: gatewayResult.InputTokens,
            CompletionTokens: gatewayResult.OutputTokens,
            TotalTokens: gatewayResult.TotalTokens,
            ModelName: modelName,
            PromptVersion: options.PromptVersion,
            CorrelationId: correlationId,
            LatencyMilliseconds: gatewayResult.LatencyMilliseconds);
    }

    private static TelegramChatbotAssistantReply BuildFallbackReply(
        TelegramBridgeAiOptions options,
        string correlationId,
        long latencyMilliseconds)
    {
        return new TelegramChatbotAssistantReply(
            MessageText: options.FallbackMessage,
            Intent: "unknown",
            NextStep: "collect_missing_data",
            Confidence: null,
            EntitiesJson: null,
            UsedFallback: true,
            UsedCache: false,
            PromptTokens: null,
            CompletionTokens: null,
            TotalTokens: null,
            ModelName: options.Model,
            PromptVersion: options.PromptVersion,
            CorrelationId: correlationId,
            LatencyMilliseconds: latencyMilliseconds);
    }

    private static IReadOnlyList<TelegramAiPromptMessage> BuildPromptMessages(
        TelegramBridgeAiOptions options,
        TelegramChatbotConversationHistoryDto? history,
        string conversationTitle,
        ChatMessageDto clientMessage)
    {
        var promptMessages = new List<TelegramAiPromptMessage>
        {
            new("system", BuildSystemPrompt(options)),
            new("system", BuildOperationalPoliciesPrompt()),
            new("system", BuildOutputContractPrompt()),
            new("system", BuildContextPrompt(options, history, conversationTitle, clientMessage))
        };

        if (!string.IsNullOrWhiteSpace(clientMessage.Text))
        {
            promptMessages.Add(new TelegramAiPromptMessage(
                "user",
                $"Mensagem atual do cliente: {clientMessage.Text.Trim()}"));
        }

        if (clientMessage.Attachments.Count > 0)
        {
            promptMessages.Add(new TelegramAiPromptMessage(
                "user",
                $"O cliente enviou {clientMessage.Attachments.Count} anexo(s). Use isso como contexto adicional."));
        }

        promptMessages.Add(new TelegramAiPromptMessage(
            "user",
            "Responda agora para o cliente seguindo o JSON exigido."));

        return promptMessages;
    }

    private static string BuildSystemPrompt(TelegramBridgeAiOptions options)
    {
        return string.IsNullOrWhiteSpace(options.SystemPrompt)
            ? "Voce e o assistente de atendimento do ConsertaPraMim. Fale em portugues-BR com tom humano e objetivo."
            : options.SystemPrompt.Trim();
    }

    private static string BuildOperationalPoliciesPrompt()
    {
        return "Politicas de resposta: " +
               "faca perguntas curtas para dados faltantes; " +
               "nao invente informacoes; " +
               "se houver incerteza, diga claramente e peca confirmacao; " +
               "priorize proximo passo pratico para triagem, abertura de pedido e agendamento.";
    }

    private static string BuildOutputContractPrompt()
    {
        return "Retorne APENAS JSON valido, sem markdown, sem texto extra, no formato: " +
               "{\"messageToClient\":\"...\",\"intent\":\"...\",\"nextStep\":\"...\",\"confidence\":0.0,\"entities\":{}}. " +
               "Campos obrigatorios: messageToClient, intent, nextStep. " +
               "confidence deve ficar entre 0 e 1 quando informado. " +
               "Quando o cliente quiser abrir pedido, use intent=open_service_request e entities com os campos: " +
               "category, problemDescription, equipment, brand, model, errorCode, zipCode, street, city, availability. " +
               "Quando o cliente quiser agendar visitas, use intent=schedule_visits e entities com os campos: " +
               "requestedVisits, preferredDays, period, preferredProviderIds.";
    }

    private static string BuildContextPrompt(
        TelegramBridgeAiOptions options,
        TelegramChatbotConversationHistoryDto? history,
        string conversationTitle,
        ChatMessageDto clientMessage)
    {
        var maxMessages = Math.Clamp(options.MaxContextMessages, 4, 40);
        var maxSnapshots = Math.Clamp(options.MaxContextSnapshots, 1, 12);
        var maxActions = Math.Clamp(options.MaxContextActionLogs, 1, 12);

        var messages = history?.Messages
            .OrderBy(item => item.SentAtUtc)
            .TakeLast(maxMessages)
            .Select(item => new
            {
                role = ResolveRole(item.Direction),
                item.Source,
                content = TrimToLimit(item.Content, 320),
                item.IntentName,
                item.SentAtUtc
            })
            .ToList() ?? [];

        var snapshots = history?.ContextSnapshots
            .OrderByDescending(item => item.CapturedAtUtc)
            .Take(maxSnapshots)
            .Select(item => new
            {
                item.SnapshotType,
                item.PromptVersion,
                item.ModelName,
                context = TrimToLimit(item.ContextJson, 450),
                item.CapturedAtUtc
            })
            .ToList() ?? [];

        var actions = history?.ActionLogs
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(maxActions)
            .Select(item => new
            {
                item.ActionType,
                item.Status,
                item.IntentName,
                result = TrimToLimit(item.ResultJson, 220),
                errorCode = item.ErrorCode,
                item.OccurredAtUtc
            })
            .ToList() ?? [];

        var contextPayload = new
        {
            conversation = new
            {
                history?.Conversation.Id,
                Title = conversationTitle,
                history?.Conversation.Status,
                history?.Conversation.LastIntent,
                history?.Conversation.LastStep,
                history?.Conversation.LastInteractionAtUtc
            },
            currentClientMessage = new
            {
                clientMessage.Id,
                text = TrimToLimit(clientMessage.Text, 1000),
                attachmentCount = clientMessage.Attachments.Count,
                clientMessage.SentAtUtc
            },
            recentMessages = messages,
            recentSnapshots = snapshots,
            recentActions = actions
        };

        var serialized = JsonSerializer.Serialize(contextPayload, JsonOptions);
        return $"Contexto operacional da conversa (JSON): {TrimToLimit(serialized, 12_000)}";
    }

    private async Task<TelegramChatbotAssistantReply> ApplyServiceRequestTriageAsync(
        string apiToken,
        Guid conversationId,
        TelegramChatbotConversationHistoryDto? history,
        ChatMessageDto clientMessage,
        TelegramChatbotAssistantReply reply,
        CancellationToken cancellationToken)
    {
        var decision = _serviceRequestTriageEngine.Evaluate(history, reply, clientMessage);
        if (!decision.IsTriageIntent)
        {
            return reply;
        }

        await PersistServiceRequestTriageStateAsync(
            apiToken,
            conversationId,
            decision.State,
            decision.MissingFields,
            reply,
            cancellationToken);

        if (decision.MissingFields.Count > 0 && !decision.State.ServiceRequestId.HasValue)
        {
            return reply with
            {
                Intent = OpenServiceRequestIntent,
                NextStep = $"collect_{decision.MissingFields[0]}",
                MessageText = string.IsNullOrWhiteSpace(decision.FollowUpMessage)
                    ? reply.MessageText
                    : decision.FollowUpMessage,
                EntitiesJson = _serviceRequestTriageEngine.SerializeEntitiesFromState(decision.State)
            };
        }

        if (decision.State.ServiceRequestId.HasValue)
        {
            return reply with
            {
                Intent = OpenServiceRequestIntent,
                NextStep = "service_request_already_created",
                MessageText = $"Seu pedido ja foi registrado com o protocolo #{decision.State.ServiceRequestId.Value.ToString("N")[..8]}. Se quiser, te atualizo o status agora.",
                EntitiesJson = _serviceRequestTriageEngine.SerializeEntitiesFromState(decision.State)
            };
        }

        if (decision.CreatePayload is null)
        {
            return reply;
        }

        var createdRequest = await _telegramChatbotApiClient.CreateServiceRequestAsync(
            apiToken,
            decision.CreatePayload,
            cancellationToken);

        if (createdRequest is null)
        {
            await _telegramChatbotApiClient.RegisterActionAsync(
                apiToken,
                conversationId,
                actionType: "open_service_request_api",
                status: ActionStatusFailed,
                intentName: OpenServiceRequestIntent,
                payloadJson: TrimToLimit(JsonSerializer.Serialize(decision.CreatePayload, JsonOptions), ContextPayloadLimit),
                resultJson: null,
                errorCode: "service_request_not_created",
                errorMessage: "Nao foi possivel criar o pedido no endpoint /api/service-requests.",
                correlationId: reply.CorrelationId,
                metadataJson: TrimToLimit(JsonSerializer.Serialize(new
                {
                    origin = "telegram_bridge",
                    stage = "st_007"
                }, JsonOptions), MetadataPayloadLimit),
                lastStep: "retry_open_service_request",
                conversationStatus: ConversationStatusActive,
                cancellationToken);

            return reply with
            {
                Intent = OpenServiceRequestIntent,
                NextStep = "retry_open_service_request",
                MessageText = "Estou com instabilidade para registrar seu pedido agora. Me confirme o CEP e eu tento novamente em seguida.",
                EntitiesJson = _serviceRequestTriageEngine.SerializeEntitiesFromState(decision.State),
                UsedFallback = true
            };
        }

        var stateWithRequest = _serviceRequestTriageEngine.MarkRequestCreated(
            decision.State,
            createdRequest.Id,
            DateTime.UtcNow);

        await PersistServiceRequestTriageStateAsync(
            apiToken,
            conversationId,
            stateWithRequest,
            [],
            reply,
            cancellationToken);

        var openPayloadJson = TrimToLimit(JsonSerializer.Serialize(new
        {
            createdAtUtc = DateTime.UtcNow,
            requestId = createdRequest.Id,
            payload = decision.CreatePayload,
            triageState = stateWithRequest
        }, JsonOptions), ContextPayloadLimit) ?? "{}";

        await _telegramChatbotApiClient.RegisterContextSnapshotAsync(
            apiToken,
            conversationId,
            snapshotType: "service_request_open_payload",
            contextJson: openPayloadJson,
            promptVersion: reply.PromptVersion,
            modelName: reply.ModelName,
            promptTokens: reply.PromptTokens,
            completionTokens: reply.CompletionTokens,
            totalTokens: reply.TotalTokens,
            intentName: OpenServiceRequestIntent,
            lastStep: "service_request_created",
            cancellationToken);

        await _telegramChatbotApiClient.RegisterActionAsync(
            apiToken,
            conversationId,
            actionType: "open_service_request_api",
            status: ActionStatusSucceeded,
            intentName: OpenServiceRequestIntent,
            payloadJson: TrimToLimit(JsonSerializer.Serialize(decision.CreatePayload, JsonOptions), ContextPayloadLimit),
            resultJson: TrimToLimit(JsonSerializer.Serialize(new
            {
                createdRequest.Id
            }, JsonOptions), ContextPayloadLimit),
            errorCode: null,
            errorMessage: null,
            correlationId: reply.CorrelationId,
            metadataJson: TrimToLimit(JsonSerializer.Serialize(new
            {
                origin = "telegram_bridge",
                stage = "st_007"
            }, JsonOptions), MetadataPayloadLimit),
            lastStep: "service_request_created",
            conversationStatus: ConversationStatusActive,
            cancellationToken);

        return reply with
        {
            Intent = OpenServiceRequestIntent,
            NextStep = "service_request_created",
            MessageText = _serviceRequestTriageEngine.BuildCreatedConfirmationMessage(stateWithRequest, createdRequest.Id),
            EntitiesJson = _serviceRequestTriageEngine.SerializeEntitiesFromState(stateWithRequest)
        };
    }

    private async Task<TelegramChatbotAssistantReply> ApplySchedulingFlowAsync(
        string apiToken,
        Guid conversationId,
        TelegramChatbotConversationHistoryDto? history,
        ChatMessageDto clientMessage,
        TelegramChatbotAssistantReply reply,
        CancellationToken cancellationToken)
    {
        var serviceRequestId = TryExtractServiceRequestId(reply.EntitiesJson);
        if (!serviceRequestId.HasValue)
        {
            return reply;
        }

        var shouldSuggestProviders =
            reply.NextStep.Equals("service_request_created", StringComparison.OrdinalIgnoreCase);

        var parseResult = _telegramSchedulingNaturalLanguageParser.Parse(clientMessage.Text, DateTime.UtcNow);
        if (!shouldSuggestProviders && !parseResult.IsSchedulingIntent)
        {
            return reply;
        }

        var providersResult = await _telegramChatbotApiClient.GetEligibleProvidersAsync(
            apiToken,
            serviceRequestId.Value,
            take: 5,
            cancellationToken);

        if (providersResult is null || !providersResult.Success)
        {
            await _telegramChatbotApiClient.RegisterActionAsync(
                apiToken,
                conversationId,
                actionType: "schedule_matching_lookup",
                status: ActionStatusFailed,
                intentName: ScheduleVisitsIntent,
                payloadJson: TrimToLimit(JsonSerializer.Serialize(new
                {
                    serviceRequestId = serviceRequestId.Value,
                    parseResult.IsSchedulingIntent,
                    parseResult.ErrorCode
                }, JsonOptions), ContextPayloadLimit),
                resultJson: null,
                errorCode: providersResult?.ErrorCode ?? "provider_lookup_failed",
                errorMessage: providersResult?.ErrorMessage ?? "Nao foi possivel listar prestadores elegiveis.",
                correlationId: reply.CorrelationId,
                metadataJson: TrimToLimit(JsonSerializer.Serialize(new
                {
                    stage = "st_008",
                    origin = "telegram_bridge"
                }, JsonOptions), MetadataPayloadLimit),
                lastStep: "provider_lookup_failed",
                conversationStatus: ConversationStatusActive,
                cancellationToken);

            if (shouldSuggestProviders)
            {
                return reply with
                {
                    Intent = ScheduleVisitsIntent,
                    NextStep = "retry_provider_matching",
                    MessageText = "Seu pedido foi registrado. Tive instabilidade para listar prestadores agora, mas posso tentar novamente em seguida.",
                    EntitiesJson = BuildSchedulingEntitiesJson(serviceRequestId.Value, [], parseResult, null)
                };
            }

            return reply;
        }

        var providers = providersResult.Providers;

        await PersistProviderSuggestionsAsync(
            apiToken,
            conversationId,
            serviceRequestId.Value,
            providers,
            reply,
            cancellationToken);

        if (providers.Count == 0)
        {
            return reply with
            {
                Intent = ScheduleVisitsIntent,
                NextStep = "no_provider_available",
                MessageText = "Registrei seu pedido, mas ainda nao encontrei prestadores disponiveis na sua regiao. Posso buscar novamente em alguns minutos.",
                EntitiesJson = BuildSchedulingEntitiesJson(serviceRequestId.Value, providers, parseResult, null)
            };
        }

        if (shouldSuggestProviders && !parseResult.IsSchedulingIntent)
        {
            return reply with
            {
                Intent = ScheduleVisitsIntent,
                NextStep = "collect_visit_windows",
                MessageText = BuildProviderSuggestionMessage(providers),
                EntitiesJson = BuildSchedulingEntitiesJson(serviceRequestId.Value, providers, parseResult, null)
            };
        }

        if (parseResult.ErrorCode is not null)
        {
            return reply with
            {
                Intent = ScheduleVisitsIntent,
                NextStep = parseResult.ErrorCode,
                MessageText = $"{BuildProviderSuggestionMessage(providers)}\n\n{parseResult.ErrorMessage}",
                EntitiesJson = BuildSchedulingEntitiesJson(serviceRequestId.Value, providers, parseResult, null)
            };
        }

        var requestedVisits = Math.Clamp(parseResult.RequestedVisits, 1, 3);
        var visitCount = Math.Min(requestedVisits, Math.Min(parseResult.Windows.Count, providers.Count));
        if (visitCount <= 0)
        {
            return reply with
            {
                Intent = ScheduleVisitsIntent,
                NextStep = "collect_visit_windows",
                MessageText = BuildProviderSuggestionMessage(providers),
                EntitiesJson = BuildSchedulingEntitiesJson(serviceRequestId.Value, providers, parseResult, null)
            };
        }

        var visitRequests = new List<TelegramChatbotBatchScheduleVisitRequestDto>(visitCount);
        for (var index = 0; index < visitCount; index++)
        {
            var provider = providers[index];
            var window = parseResult.Windows[index];

            visitRequests.Add(new TelegramChatbotBatchScheduleVisitRequestDto(
                ProviderId: provider.ProviderId,
                WindowStartUtc: window.WindowStartUtc,
                WindowEndUtc: window.WindowEndUtc,
                Reason: "Agendamento solicitado em linguagem natural pelo chatbot Telegram."));
        }

        var batchResult = await _telegramChatbotApiClient.ScheduleVisitsBatchAsync(
            apiToken,
            serviceRequestId.Value,
            visitRequests,
            cancellationToken);

        if (batchResult is null)
        {
            await _telegramChatbotApiClient.RegisterActionAsync(
                apiToken,
                conversationId,
                actionType: "schedule_batch_create",
                status: ActionStatusFailed,
                intentName: ScheduleVisitsIntent,
                payloadJson: TrimToLimit(JsonSerializer.Serialize(new
                {
                    serviceRequestId = serviceRequestId.Value,
                    visits = visitRequests
                }, JsonOptions), ContextPayloadLimit),
                resultJson: null,
                errorCode: "schedule_batch_failed",
                errorMessage: "Nao foi possivel processar o agendamento em lote.",
                correlationId: reply.CorrelationId,
                metadataJson: TrimToLimit(JsonSerializer.Serialize(new
                {
                    stage = "st_008",
                    origin = "telegram_bridge"
                }, JsonOptions), MetadataPayloadLimit),
                lastStep: "schedule_batch_failed",
                conversationStatus: ConversationStatusActive,
                cancellationToken);

            return reply with
            {
                Intent = ScheduleVisitsIntent,
                NextStep = "retry_schedule_batch",
                MessageText = "Entendi os dias e periodos, mas tive instabilidade para concluir os agendamentos agora. Posso tentar novamente em seguida.",
                EntitiesJson = BuildSchedulingEntitiesJson(serviceRequestId.Value, providers, parseResult, null),
                UsedFallback = true
            };
        }

        await PersistSchedulingBatchResultAsync(
            apiToken,
            conversationId,
            serviceRequestId.Value,
            visitRequests,
            batchResult,
            reply,
            cancellationToken);

        var nextStep = batchResult.Success
            ? "visits_scheduled"
            : "visits_partial_or_failed";

        return reply with
        {
            Intent = ScheduleVisitsIntent,
            NextStep = nextStep,
            MessageText = BuildBatchSchedulingMessage(providers, batchResult),
            EntitiesJson = BuildSchedulingEntitiesJson(serviceRequestId.Value, providers, parseResult, batchResult)
        };
    }

    private async Task PersistProviderSuggestionsAsync(
        string apiToken,
        Guid conversationId,
        Guid serviceRequestId,
        IReadOnlyList<TelegramChatbotEligibleProviderDto> providers,
        TelegramChatbotAssistantReply reply,
        CancellationToken cancellationToken)
    {
        var snapshotPayload = new
        {
            capturedAtUtc = DateTime.UtcNow,
            serviceRequestId,
            suggestedProviders = providers.Select(item => new
            {
                item.ProviderId,
                item.ProviderName,
                item.DistanceKm,
                item.Rating,
                item.ReviewCount
            }).ToList()
        };

        await _telegramChatbotApiClient.RegisterContextSnapshotAsync(
            apiToken,
            conversationId,
            snapshotType: "scheduling_provider_suggestions",
            contextJson: TrimToLimit(JsonSerializer.Serialize(snapshotPayload, JsonOptions), ContextPayloadLimit) ?? "{}",
            promptVersion: reply.PromptVersion,
            modelName: reply.ModelName,
            promptTokens: reply.PromptTokens,
            completionTokens: reply.CompletionTokens,
            totalTokens: reply.TotalTokens,
            intentName: ScheduleVisitsIntent,
            lastStep: "providers_suggested",
            cancellationToken);

        await _telegramChatbotApiClient.RegisterActionAsync(
            apiToken,
            conversationId,
            actionType: "schedule_matching_lookup",
            status: ActionStatusSucceeded,
            intentName: ScheduleVisitsIntent,
            payloadJson: TrimToLimit(JsonSerializer.Serialize(new
            {
                serviceRequestId
            }, JsonOptions), ContextPayloadLimit),
            resultJson: TrimToLimit(JsonSerializer.Serialize(new
            {
                providerCount = providers.Count
            }, JsonOptions), ContextPayloadLimit),
            errorCode: null,
            errorMessage: null,
            correlationId: reply.CorrelationId,
            metadataJson: TrimToLimit(JsonSerializer.Serialize(new
            {
                stage = "st_008",
                origin = "telegram_bridge"
            }, JsonOptions), MetadataPayloadLimit),
            lastStep: "providers_suggested",
            conversationStatus: ConversationStatusActive,
            cancellationToken);
    }

    private async Task PersistSchedulingBatchResultAsync(
        string apiToken,
        Guid conversationId,
        Guid serviceRequestId,
        IReadOnlyList<TelegramChatbotBatchScheduleVisitRequestDto> visitRequests,
        TelegramChatbotBatchScheduleResultDto batchResult,
        TelegramChatbotAssistantReply reply,
        CancellationToken cancellationToken)
    {
        var snapshotPayload = new
        {
            capturedAtUtc = DateTime.UtcNow,
            serviceRequestId,
            request = visitRequests,
            result = batchResult
        };

        await _telegramChatbotApiClient.RegisterContextSnapshotAsync(
            apiToken,
            conversationId,
            snapshotType: "scheduling_batch_result",
            contextJson: TrimToLimit(JsonSerializer.Serialize(snapshotPayload, JsonOptions), ContextPayloadLimit) ?? "{}",
            promptVersion: reply.PromptVersion,
            modelName: reply.ModelName,
            promptTokens: reply.PromptTokens,
            completionTokens: reply.CompletionTokens,
            totalTokens: reply.TotalTokens,
            intentName: ScheduleVisitsIntent,
            lastStep: batchResult.Success ? "visits_scheduled" : "visits_partial_or_failed",
            cancellationToken);

        await _telegramChatbotApiClient.RegisterActionAsync(
            apiToken,
            conversationId,
            actionType: "schedule_batch_create",
            status: batchResult.Success ? ActionStatusSucceeded : ActionStatusFailed,
            intentName: ScheduleVisitsIntent,
            payloadJson: TrimToLimit(JsonSerializer.Serialize(new
            {
                serviceRequestId,
                visits = visitRequests
            }, JsonOptions), ContextPayloadLimit),
            resultJson: TrimToLimit(JsonSerializer.Serialize(batchResult, JsonOptions), ContextPayloadLimit),
            errorCode: batchResult.ErrorCode,
            errorMessage: batchResult.ErrorMessage,
            correlationId: reply.CorrelationId,
            metadataJson: TrimToLimit(JsonSerializer.Serialize(new
            {
                stage = "st_008",
                origin = "telegram_bridge"
            }, JsonOptions), MetadataPayloadLimit),
            lastStep: batchResult.Success ? "visits_scheduled" : "visits_partial_or_failed",
            conversationStatus: ConversationStatusActive,
            cancellationToken);
    }

    private static string BuildProviderSuggestionMessage(IReadOnlyList<TelegramChatbotEligibleProviderDto> providers)
    {
        var topProviders = providers
            .Take(3)
            .Select((item, index) =>
                $"{index + 1}. {item.ProviderName} ({item.DistanceKm:0.0} km, nota {item.Rating:0.0})")
            .ToList();

        return "Encontrei estes prestadores disponiveis na sua regiao:\n" +
               string.Join("\n", topProviders) +
               "\n\nSe quiser, posso agendar ate 3 visitas em dias diferentes. Me diga os dias e o periodo (ex.: quarta e sexta de manha).";
    }

    private static string BuildBatchSchedulingMessage(
        IReadOnlyList<TelegramChatbotEligibleProviderDto> providers,
        TelegramChatbotBatchScheduleResultDto batchResult)
    {
        var providerNames = providers.ToDictionary(item => item.ProviderId, item => item.ProviderName);
        var successLines = new List<string>();
        var failureLines = new List<string>();

        foreach (var item in batchResult.Results)
        {
            var providerName = providerNames.TryGetValue(item.ProviderId, out var resolvedName)
                ? resolvedName
                : "Prestador";

            var windowLabel = BuildWindowLabel(item.WindowStartUtc, item.WindowEndUtc);
            if (item.Success)
            {
                successLines.Add($"- {providerName}: {windowLabel}");
            }
            else
            {
                var reason = string.IsNullOrWhiteSpace(item.ErrorMessage)
                    ? "indisponibilidade de agenda"
                    : item.ErrorMessage.Trim();
                failureLines.Add($"- {providerName}: {reason}");
            }
        }

        if (successLines.Count > 0 && failureLines.Count == 0)
        {
            return "Perfeito! Agendamentos solicitados com sucesso:\n" +
                   string.Join("\n", successLines) +
                   "\n\nVou acompanhar a confirmacao e te aviso por aqui.";
        }

        if (successLines.Count > 0)
        {
            return "Consegui agendar parte das visitas:\n" +
                   string.Join("\n", successLines) +
                   "\n\nAs demais tiveram conflito:\n" +
                   string.Join("\n", failureLines) +
                   "\n\nSe quiser, me diga novos dias/periodos para tentar novamente.";
        }

        return "Nao consegui confirmar os agendamentos nessas janelas:\n" +
               string.Join("\n", failureLines) +
               "\n\nMe passe novos dias/periodos e eu tento novamente.";
    }

    private static string BuildWindowLabel(DateTime windowStartUtc, DateTime windowEndUtc)
    {
        var startUtc = windowStartUtc.Kind == DateTimeKind.Utc
            ? windowStartUtc
            : DateTime.SpecifyKind(windowStartUtc, DateTimeKind.Utc);
        var endUtc = windowEndUtc.Kind == DateTimeKind.Utc
            ? windowEndUtc
            : DateTime.SpecifyKind(windowEndUtc, DateTimeKind.Utc);

        var startLocal = TimeZoneInfo.ConvertTimeFromUtc(startUtc, BusinessTimeZone);
        var endLocal = TimeZoneInfo.ConvertTimeFromUtc(endUtc, BusinessTimeZone);
        return $"{startLocal:ddd dd/MM HH:mm} - {endLocal:HH:mm} (America/Sao_Paulo)";
    }

    private static string BuildSchedulingEntitiesJson(
        Guid serviceRequestId,
        IReadOnlyList<TelegramChatbotEligibleProviderDto> providers,
        TelegramSchedulingParseResult parseResult,
        TelegramChatbotBatchScheduleResultDto? batchResult)
    {
        var payload = new
        {
            serviceRequestId,
            requestedVisits = parseResult.RequestedVisits,
            parseResult.ErrorCode,
            parseResult.ErrorMessage,
            windows = parseResult.Windows.Select(item => new
            {
                item.DayLabel,
                item.PeriodLabel,
                item.WindowStartUtc,
                item.WindowEndUtc
            }).ToList(),
            suggestedProviders = providers.Select(item => new
            {
                item.ProviderId,
                item.ProviderName,
                item.DistanceKm,
                item.Rating
            }).ToList(),
            batch = batchResult
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static Guid? TryExtractServiceRequestId(string? entitiesJson)
    {
        if (string.IsNullOrWhiteSpace(entitiesJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(entitiesJson);
            if (!TryGetPropertyIgnoreCase(document.RootElement, "serviceRequestId", out var requestIdElement))
            {
                return null;
            }

            var raw = requestIdElement.ValueKind switch
            {
                JsonValueKind.String => requestIdElement.GetString(),
                JsonValueKind.Number => requestIdElement.GetRawText(),
                _ => null
            };

            return Guid.TryParse(raw, out var requestId)
                ? requestId
                : (Guid?)null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement root,
        string propertyName,
        out JsonElement value)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        if (root.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private async Task PersistServiceRequestTriageStateAsync(
        string apiToken,
        Guid conversationId,
        TelegramServiceRequestTriageState state,
        IReadOnlyList<string> missingFields,
        TelegramChatbotAssistantReply reply,
        CancellationToken cancellationToken)
    {
        var snapshot = new
        {
            capturedAtUtc = DateTime.UtcNow,
            state,
            missingFields
        };

        await _telegramChatbotApiClient.RegisterContextSnapshotAsync(
            apiToken,
            conversationId,
            snapshotType: "service_request_triage_state",
            contextJson: TrimToLimit(JsonSerializer.Serialize(snapshot, JsonOptions), ContextPayloadLimit) ?? "{}",
            promptVersion: reply.PromptVersion,
            modelName: reply.ModelName,
            promptTokens: reply.PromptTokens,
            completionTokens: reply.CompletionTokens,
            totalTokens: reply.TotalTokens,
            intentName: OpenServiceRequestIntent,
            lastStep: missingFields.Count == 0 ? "triage_complete" : $"triage_missing_{missingFields[0]}",
            cancellationToken);
    }

    private async Task PersistOrchestrationTrailAsync(
        string apiToken,
        Guid conversationId,
        TelegramChatbotConversationHistoryDto? history,
        IReadOnlyList<TelegramAiPromptMessage> promptMessages,
        TelegramChatbotAssistantReply reply,
        TelegramAiGatewayResult gatewayResult,
        TelegramBridgeAiOptions options,
        CancellationToken cancellationToken)
    {
        var promptPreview = promptMessages
            .Select(item => new
            {
                item.Role,
                content = TrimToLimit(item.Content, 420)
            })
            .ToList();

        var snapshotPayload = new
        {
            generatedAtUtc = DateTime.UtcNow,
            reply.CorrelationId,
            model = reply.ModelName,
            promptVersion = reply.PromptVersion,
            reply.Intent,
            reply.NextStep,
            reply.Confidence,
            reply.EntitiesJson,
            reply.UsedFallback,
            reply.UsedCache,
            history = new
            {
                conversationId,
                history?.Conversation.LastIntent,
                history?.Conversation.LastStep,
                messageCount = history?.Messages.Count ?? 0,
                snapshotCount = history?.ContextSnapshots.Count ?? 0,
                actionCount = history?.ActionLogs.Count ?? 0
            },
            prompt = promptPreview,
            gateway = new
            {
                gatewayResult.Success,
                gatewayResult.ErrorCode,
                gatewayResult.ErrorMessage,
                gatewayResult.AttemptCount,
                gatewayResult.LatencyMilliseconds,
                gatewayResult.InputTokens,
                gatewayResult.OutputTokens,
                gatewayResult.TotalTokens
            }
        };

        await _telegramChatbotApiClient.RegisterContextSnapshotAsync(
            apiToken,
            conversationId,
            snapshotType: "ai_orchestration_context",
            contextJson: TrimToLimit(JsonSerializer.Serialize(snapshotPayload, JsonOptions), ContextPayloadLimit) ?? "{}",
            promptVersion: options.PromptVersion,
            modelName: reply.ModelName,
            promptTokens: reply.PromptTokens,
            completionTokens: reply.CompletionTokens,
            totalTokens: reply.TotalTokens,
            intentName: reply.Intent,
            lastStep: reply.NextStep,
            cancellationToken);

        var actionPayload = new
        {
            conversationId,
            reply.CorrelationId,
            model = reply.ModelName,
            promptVersion = reply.PromptVersion,
            gatewayResult.AttemptCount,
            gatewayResult.Success,
            gatewayResult.ErrorCode,
            gatewayResult.ErrorMessage,
            gatewayResult.LatencyMilliseconds
        };

        var actionResult = new
        {
            reply.MessageText,
            reply.Intent,
            reply.NextStep,
            reply.Confidence,
            reply.UsedFallback,
            reply.UsedCache,
            reply.PromptTokens,
            reply.CompletionTokens,
            reply.TotalTokens,
            reply.LatencyMilliseconds
        };

        await _telegramChatbotApiClient.RegisterActionAsync(
            apiToken,
            conversationId,
            actionType: "openai_generate_reply",
            status: reply.UsedFallback ? ActionStatusFailed : ActionStatusSucceeded,
            intentName: reply.Intent,
            payloadJson: TrimToLimit(JsonSerializer.Serialize(actionPayload, JsonOptions), ContextPayloadLimit),
            resultJson: TrimToLimit(JsonSerializer.Serialize(actionResult, JsonOptions), ContextPayloadLimit),
            errorCode: gatewayResult.Success ? null : gatewayResult.ErrorCode,
            errorMessage: gatewayResult.Success ? null : gatewayResult.ErrorMessage,
            correlationId: reply.CorrelationId,
            metadataJson: TrimToLimit(
                JsonSerializer.Serialize(new
                {
                    origin = "telegram_bridge",
                    stage = "st_006",
                    promptVersion = reply.PromptVersion
                }, JsonOptions),
                MetadataPayloadLimit),
            lastStep: reply.NextStep,
            conversationStatus: ConversationStatusActive,
            cancellationToken);

        await _telegramChatbotApiClient.UpdateConversationStateAsync(
            apiToken,
            conversationId,
            status: ConversationStatusActive,
            lastIntent: reply.Intent,
            lastStep: reply.NextStep,
            metadataJson: TrimToLimit(
                JsonSerializer.Serialize(new
                {
                    model = reply.ModelName,
                    promptVersion = reply.PromptVersion,
                    reply.CorrelationId,
                    reply.UsedFallback,
                    reply.UsedCache,
                    reply.Confidence,
                    entities = reply.EntitiesJson
                }, JsonOptions),
                MetadataPayloadLimit),
            cancellationToken);
    }

    private async Task PersistUnexpectedFailureActionAsync(
        string apiToken,
        Guid conversationId,
        TelegramChatbotAssistantReply fallback,
        Exception exception,
        CancellationToken cancellationToken)
    {
        await _telegramChatbotApiClient.RegisterActionAsync(
            apiToken,
            conversationId,
            actionType: "openai_generate_reply",
            status: ActionStatusFailed,
            intentName: fallback.Intent,
            payloadJson: null,
            resultJson: TrimToLimit(JsonSerializer.Serialize(new
            {
                fallback.MessageText,
                fallback.Intent,
                fallback.NextStep,
                fallback.CorrelationId
            }, JsonOptions), ContextPayloadLimit),
            errorCode: "orchestrator_unhandled_exception",
            errorMessage: TrimToLimit(exception.Message, 1000),
            correlationId: fallback.CorrelationId,
            metadataJson: TrimToLimit(JsonSerializer.Serialize(new { stage = "st_006", origin = "telegram_bridge" }, JsonOptions), MetadataPayloadLimit),
            lastStep: fallback.NextStep,
            conversationStatus: ConversationStatusActive,
            cancellationToken);
    }

    private static void PublishMetrics(
        string modelName,
        TelegramChatbotAssistantReply reply,
        TelegramAiGatewayResult gatewayResult,
        long fallbackLatencyMilliseconds)
    {
        var latency = gatewayResult.LatencyMilliseconds > 0
            ? gatewayResult.LatencyMilliseconds
            : fallbackLatencyMilliseconds;

        RequestCounter.Add(1,
            new KeyValuePair<string, object?>("model", modelName),
            new KeyValuePair<string, object?>("intent", reply.Intent),
            new KeyValuePair<string, object?>("used_cache", reply.UsedCache),
            new KeyValuePair<string, object?>("used_fallback", reply.UsedFallback));

        LatencyHistogram.Record(latency,
            new KeyValuePair<string, object?>("model", modelName),
            new KeyValuePair<string, object?>("used_fallback", reply.UsedFallback));

        if (reply.TotalTokens.HasValue)
        {
            TokensHistogram.Record(reply.TotalTokens.Value,
                new KeyValuePair<string, object?>("model", modelName),
                new KeyValuePair<string, object?>("intent", reply.Intent));
        }

        if (reply.UsedFallback)
        {
            FallbackCounter.Add(1,
                new KeyValuePair<string, object?>("model", modelName),
                new KeyValuePair<string, object?>("intent", reply.Intent));
        }

        if (!gatewayResult.Success)
        {
            FailureCounter.Add(1,
                new KeyValuePair<string, object?>("model", modelName),
                new KeyValuePair<string, object?>("error_code", gatewayResult.ErrorCode ?? "unknown"));
        }
    }

    private static TimeZoneInfo ResolveBusinessTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static string CreateCorrelationId()
    {
        return Guid.NewGuid().ToString("N")[..16];
    }

    private static string BuildCacheKey(
        Guid conversationId,
        string? clientMessage,
        int attachmentCount,
        string? promptVersion,
        string modelName)
    {
        var normalizedMessage = string.IsNullOrWhiteSpace(clientMessage)
            ? "(no_text)"
            : clientMessage.Trim();

        var payload = $"{conversationId:D}|{normalizedMessage}|{attachmentCount}|{promptVersion}|{modelName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return $"telegram-ai:{Convert.ToHexString(hash)}";
    }

    private static string? TrimToLimit(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed[..maxLength];
    }

    private static string ResolveRole(int direction)
    {
        return direction switch
        {
            1 => "user",
            2 => "assistant",
            _ => "system"
        };
    }
}
