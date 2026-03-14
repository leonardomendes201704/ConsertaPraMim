using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private const string ListOrdersIntent = "list_orders";
    private const string GetOrderStatusIntent = "get_order_status";
    private const string GetOrderDetailsIntent = "get_order_details";
    private const string ListAppointmentsIntent = "list_appointments";
    private const string HumanHandoffIntent = "human_handoff";
    private const string ClientsBoardType = "clientes";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();
    private static readonly Regex ProtocolRegex = new(
        "(?:#|protocolo\\s*)?(?<protocol>[a-f0-9]{8})\\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
    private readonly ITelegramLeadAutomationClient _telegramLeadAutomationClient;
    private readonly TelegramAutomationOptions _telegramAutomationOptions;
    private readonly ITelegramChatbotFeatureFlagService _featureFlagService;
    private readonly ITelegramChatbotObservabilityService _observabilityService;

    public TelegramChatbotOrchestrator(
        ITelegramAiGateway telegramAiGateway,
        ITelegramChatbotApiClient telegramChatbotApiClient,
        IOptions<TelegramBridgeAiOptions> options,
        IMemoryCache memoryCache,
        ILogger<TelegramChatbotOrchestrator> logger,
        TelegramServiceRequestTriageEngine serviceRequestTriageEngine,
        TelegramSchedulingNaturalLanguageParser telegramSchedulingNaturalLanguageParser,
        ITelegramLeadAutomationClient? telegramLeadAutomationClient = null,
        IOptions<TelegramAutomationOptions>? telegramAutomationOptions = null,
        ITelegramChatbotFeatureFlagService? featureFlagService = null,
        ITelegramChatbotObservabilityService? observabilityService = null)
    {
        _telegramAiGateway = telegramAiGateway;
        _telegramChatbotApiClient = telegramChatbotApiClient;
        _options = options;
        _memoryCache = memoryCache;
        _logger = logger;
        _serviceRequestTriageEngine = serviceRequestTriageEngine;
        _telegramSchedulingNaturalLanguageParser = telegramSchedulingNaturalLanguageParser;
        _telegramLeadAutomationClient = telegramLeadAutomationClient ?? NullTelegramLeadAutomationClient.Instance;
        _telegramAutomationOptions = telegramAutomationOptions?.Value ?? new TelegramAutomationOptions();
        _featureFlagService = featureFlagService ?? NullTelegramChatbotFeatureFlagService.Instance;
        _observabilityService = observabilityService ?? NullTelegramChatbotObservabilityService.Instance;
    }

    public async Task<TelegramChatbotAssistantReply?> GenerateAssistantReplyAsync(
        string apiToken,
        long chatId,
        ChatMessageDto clientMessage,
        string conversationTitle,
        CancellationToken cancellationToken = default,
        Guid? clientId = null,
        string? clientEmail = null)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            return null;
        }

        var correlationId = CreateCorrelationId();
        var modelName = string.IsNullOrWhiteSpace(options.Model)
            ? "gpt-4.1-mini"
            : options.Model.Trim();

        var rolloutDecision = _featureFlagService.Evaluate(chatId);
        if (!rolloutDecision.IsEnabled)
        {
            _logger.LogInformation(
                "Chatbot IA bloqueado por feature flag. ChatId: {ChatId}. Reason: {ReasonCode}. Bucket: {Bucket}.",
                chatId,
                rolloutDecision.ReasonCode,
                rolloutDecision.Bucket);

            _observabilityService.RecordIncident(
                stage: "feature_flag",
                errorCode: rolloutDecision.ReasonCode,
                correlationId: correlationId,
                message: "Chat bloqueado por rollout/feature flag.");

            var rolloutFallback = BuildFallbackReply(
                options,
                correlationId,
                latencyMilliseconds: 0,
                errorCode: rolloutDecision.ReasonCode);

            if (rolloutFallback.NextStep.StartsWith("handoff_", StringComparison.OrdinalIgnoreCase))
            {
                _observabilityService.RecordBusinessEvent("human_handoff", success: true);
            }

            return rolloutFallback;
        }

        var provider = string.IsNullOrWhiteSpace(options.Provider)
            ? "OpenAI"
            : options.Provider.Trim();

        if (!provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Provider de IA nao suportado na bridge. Provider: {Provider}", provider);
            _observabilityService.RecordIncident(
                stage: "gateway_provider",
                errorCode: "provider_not_supported",
                correlationId: correlationId,
                message: $"Provider de IA nao suportado: {provider}");

            return BuildFallbackReply(options, correlationId, latencyMilliseconds: 0, errorCode: "provider_not_supported");
        }

        var normalizedTitle = string.IsNullOrWhiteSpace(conversationTitle)
            ? $"Atendimento {chatId}"
            : conversationTitle.Trim();

        var sessionDependencyStart = Stopwatch.StartNew();
        var conversationId = await _telegramChatbotApiClient.OpenOrResumeSessionAsync(
            apiToken,
            chatId,
            normalizedTitle,
            cancellationToken);
        sessionDependencyStart.Stop();
        _observabilityService.RecordDependency(
            dependency: "api.telegram_chatbot.session",
            success: conversationId.HasValue,
            latencyMilliseconds: sessionDependencyStart.ElapsedMilliseconds,
            errorCode: conversationId.HasValue ? null : "conversation_open_failed");

        if (!conversationId.HasValue)
        {
            _logger.LogWarning("Nao foi possivel abrir/retomar conversa para orquestracao IA. ChatId: {ChatId}", chatId);
            _observabilityService.RecordIncident(
                stage: "session_open",
                errorCode: "conversation_open_failed",
                correlationId: correlationId,
                message: $"Falha ao abrir sessao para chat {chatId}.");
            return BuildFallbackReply(options, correlationId, latencyMilliseconds: 0, errorCode: "conversation_open_failed");
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

            _observabilityService.RecordAiOutcome(
                replayed,
                new TelegramAiGatewayResult(
                    Success: true,
                    AttemptCount: 0,
                    LatencyMilliseconds: replayed.LatencyMilliseconds));

            return replayed;
        }

        var startedAt = Stopwatch.StartNew();

        TelegramChatbotConversationHistoryDto? history = null;
        TelegramAiGatewayResult gatewayResult;
        TelegramChatbotAssistantReply reply;

        try
        {
            var historyDependencyStart = Stopwatch.StartNew();
            history = await _telegramChatbotApiClient.GetConversationHistoryAsync(
                apiToken,
                conversationId.Value,
                options.MaxContextMessages,
                options.MaxContextSnapshots,
                options.MaxContextActionLogs,
                cancellationToken);
            historyDependencyStart.Stop();
            _observabilityService.RecordDependency(
                dependency: "api.telegram_chatbot.history",
                success: history is not null,
                latencyMilliseconds: historyDependencyStart.ElapsedMilliseconds,
                errorCode: history is null ? "history_not_available" : null);

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

            _observabilityService.RecordDependency(
                dependency: "openai.responses",
                success: gatewayResult.Success,
                latencyMilliseconds: gatewayResult.LatencyMilliseconds,
                errorCode: gatewayResult.Success ? null : gatewayResult.ErrorCode ?? "openai_request_failed");

            if (!gatewayResult.Success)
            {
                _observabilityService.RecordIncident(
                    stage: "openai_gateway",
                    errorCode: gatewayResult.ErrorCode ?? "openai_request_failed",
                    correlationId: correlationId,
                    message: gatewayResult.ErrorMessage);
            }

            reply = BuildAssistantReplyFromGateway(options, gatewayResult, modelName, correlationId);

            reply = await ApplyServiceRequestTriageAsync(
                apiToken,
                conversationId.Value,
                history,
                clientMessage,
                reply,
                clientId,
                clientEmail,
                cancellationToken);

            reply = await ApplyNaturalQueryFlowAsync(
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

            var guardrailDecision = TelegramChatbotGuardrailPolicy.Evaluate(clientMessage, reply);
            if (guardrailDecision.Triggered)
            {
                reply = reply with
                {
                    MessageText = guardrailDecision.MessageToClient,
                    Intent = guardrailDecision.RequiresHumanHandoff ? HumanHandoffIntent : reply.Intent,
                    NextStep = guardrailDecision.NextStep,
                    EntitiesJson = BuildGuardrailEntitiesJson(
                        guardrailDecision.RuleCode,
                        guardrailDecision.Reason,
                        reply.Intent,
                        reply.NextStep),
                    UsedFallback = true
                };

                await PersistGuardrailInterventionAsync(
                    apiToken,
                    conversationId.Value,
                    guardrailDecision,
                    reply,
                    cancellationToken);

                _observabilityService.RecordBusinessEvent("guardrail_intervention", success: true);
                if (guardrailDecision.RequiresHumanHandoff)
                {
                    _observabilityService.RecordBusinessEvent("human_handoff", success: true);
                }
            }

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
            _observabilityService.RecordAiOutcome(reply, gatewayResult);

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

            _observabilityService.RecordIncident(
                stage: "orchestrator_exception",
                errorCode: "orchestrator_unhandled_exception",
                correlationId: correlationId,
                message: exception.Message);

            var fallback = BuildFallbackReply(
                options,
                correlationId,
                startedAt.ElapsedMilliseconds,
                errorCode: "orchestrator_unhandled_exception");

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
            return BuildFallbackReply(
                options,
                correlationId,
                gatewayResult.LatencyMilliseconds,
                errorCode: gatewayResult.ErrorCode);
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
        long latencyMilliseconds,
        string? errorCode = null)
    {
        var fallback = TelegramChatbotErrorCatalog.Resolve(errorCode, options.FallbackMessage);
        var fallbackMessage = fallback.ClientMessage;

        var shouldUseConfiguredFallback =
            !string.IsNullOrWhiteSpace(options.FallbackMessage) &&
            (string.IsNullOrWhiteSpace(errorCode) ||
             errorCode.StartsWith("openai_", StringComparison.OrdinalIgnoreCase) ||
             errorCode.Equals("orchestrator_unhandled_exception", StringComparison.OrdinalIgnoreCase));

        if (shouldUseConfiguredFallback)
        {
            fallbackMessage = options.FallbackMessage.Trim();
        }

        return new TelegramChatbotAssistantReply(
            MessageText: fallbackMessage,
            Intent: fallback.RequiresHumanHandoff ? HumanHandoffIntent : "unknown",
            NextStep: fallback.NextStep,
            Confidence: null,
            EntitiesJson: BuildFallbackEntitiesJson(fallback.ErrorCode),
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
            new("system", BuildGuardrailsPrompt()),
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
               "priorize proximo passo pratico para triagem, abertura de pedido, agendamento e consultas de status.";
    }

    private static string BuildGuardrailsPrompt()
    {
        return "Guardrails obrigatorios: " +
               "nao solicitar senha, codigo de seguranca, numero completo de cartao ou token bancario; " +
               "nao orientar temas medicos, juridicos, financeiros ou fora do escopo de consertos; " +
               "em risco de seguranca (fogo, choque eletrico, vazamento de gas), orientar emergencia local e handoff humano; " +
               "quando precisar escalar para humano, use intent=human_handoff e nextStep iniciando com handoff_.";
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
               "requestedVisits, preferredDays, period, preferredProviderIds. " +
               "Quando o cliente quiser consultar pedidos, use intent=list_orders. " +
               "Quando o cliente quiser status de um pedido, use intent=get_order_status. " +
               "Quando o cliente quiser detalhes de um pedido, use intent=get_order_details. " +
               "Quando o cliente quiser consultar agenda, use intent=list_appointments. " +
               "Quando precisar escalar para humano, use intent=human_handoff. " +
               "Para consultas especificas, em entities use serviceRequestId e/ou protocol quando disponivel.";
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
        Guid? clientId,
        string? clientEmail,
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
            await TryAutomateTelegramClientLeadAsync(
                apiToken,
                conversationId,
                clientMessage,
                reply,
                decision.State,
                clientId,
                clientEmail,
                cancellationToken);

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

        var createRequestDependencyStart = Stopwatch.StartNew();
        var createdRequest = await _telegramChatbotApiClient.CreateServiceRequestAsync(
            apiToken,
            decision.CreatePayload,
            cancellationToken);
        createRequestDependencyStart.Stop();
        _observabilityService.RecordDependency(
            dependency: "api.service_requests.create",
            success: createdRequest is not null,
            latencyMilliseconds: createRequestDependencyStart.ElapsedMilliseconds,
            errorCode: createdRequest is null ? "service_request_not_created" : null);

        if (createdRequest is null)
        {
            _observabilityService.RecordIncident(
                stage: "service_request_create",
                errorCode: "service_request_not_created",
                correlationId: reply.CorrelationId,
                message: "POST /api/service-requests retornou vazio para chatbot.");

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

        _observabilityService.RecordBusinessEvent("triage_request_opened", success: true);

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

        await TryAutomateTelegramClientLeadAsync(
            apiToken,
            conversationId,
            clientMessage,
            reply,
            stateWithRequest,
            clientId,
            clientEmail,
            cancellationToken);

        return reply with
        {
            Intent = OpenServiceRequestIntent,
            NextStep = "service_request_created",
            MessageText = _serviceRequestTriageEngine.BuildCreatedConfirmationMessage(stateWithRequest, createdRequest.Id),
            EntitiesJson = _serviceRequestTriageEngine.SerializeEntitiesFromState(stateWithRequest)
        };
    }

    private async Task TryAutomateTelegramClientLeadAsync(
        string apiToken,
        Guid conversationId,
        ChatMessageDto clientMessage,
        TelegramChatbotAssistantReply reply,
        TelegramServiceRequestTriageState triageState,
        Guid? clientId,
        string? clientEmail,
        CancellationToken cancellationToken)
    {
        if (!_telegramAutomationOptions.Enabled || !_telegramAutomationOptions.ClientsAutomationEnabled)
        {
            return;
        }

        if (!clientId.HasValue || clientId.Value == Guid.Empty || !triageState.ServiceRequestId.HasValue)
        {
            return;
        }

        var automationRequest = BuildTelegramLeadAutomationRequest(
            conversationId,
            clientMessage,
            triageState,
            clientId.Value,
            clientEmail);
        var automationResult = await _telegramLeadAutomationClient.UpsertClientLeadAsync(automationRequest, cancellationToken);

        await _telegramChatbotApiClient.RegisterActionAsync(
            apiToken,
            conversationId,
            actionType: "telegram_funil_automation",
            status: automationResult.Success ? ActionStatusSucceeded : ActionStatusFailed,
            intentName: OpenServiceRequestIntent,
            payloadJson: TrimToLimit(JsonSerializer.Serialize(automationRequest, JsonOptions), ContextPayloadLimit),
            resultJson: TrimToLimit(JsonSerializer.Serialize(new
            {
                automationResult.Success,
                automationResult.HttpStatusCode,
                automationResult.LeadId,
                automationResult.Created,
                automationResult.BoardType,
                automationResult.ChatwootStatus,
                automationResult.ChatwootConversationId,
                automationResult.ChatwootInboxId
            }, JsonOptions), ContextPayloadLimit),
                errorCode: automationResult.Success ? null : "telegram_funil_automation_failed",
                errorMessage: automationResult.Success ? null : automationResult.Message,
                correlationId: reply.CorrelationId,
                metadataJson: TrimToLimit(JsonSerializer.Serialize(new
                {
                    origin = "telegram_bridge",
                    stage = "epic_telegram_001",
                    boardType = ClientsBoardType
                }, JsonOptions), MetadataPayloadLimit),
                lastStep: automationResult.Success ? "telegram_lead_synced" : "telegram_lead_sync_failed",
                conversationStatus: ConversationStatusActive,
                cancellationToken);

        if (!automationResult.Success)
        {
            _observabilityService.RecordIncident(
                stage: "telegram_funil_automation",
                errorCode: "telegram_funil_automation_failed",
                correlationId: reply.CorrelationId,
                message: automationResult.Message);
        }
    }

    private static TelegramLeadAutomationUpsertRequest BuildTelegramLeadAutomationRequest(
        Guid chatbotConversationId,
        ChatMessageDto clientMessage,
        TelegramServiceRequestTriageState triageState,
        Guid clientId,
        string? clientEmail)
    {
        var protocol = triageState.ServiceRequestId?.ToString("N")[..8];
        var description = string.IsNullOrWhiteSpace(triageState.ProblemDescription)
            ? "Lead originado automaticamente pelo bot Telegram."
            : $"Lead originado automaticamente pelo bot Telegram. Contexto inicial: {triageState.ProblemDescription.Trim()}";
        var internalNotes = $"""
Origem automatica: bot Telegram
ChatbotConversationId: {chatbotConversationId}
TelegramChatId: {clientMessage.ChatId}
ServiceRequestId: {triageState.ServiceRequestId}
Ultima mensagem do cliente: {triageState.LastClientMessage}
""";

        return new TelegramLeadAutomationUpsertRequest
        {
            BoardType = ClientsBoardType,
            ChatbotConversationId = chatbotConversationId,
            ChannelConversationId = clientMessage.ChatId.ToString(CultureInfo.InvariantCulture),
            TelegramChatId = clientMessage.ChatId,
            ClientId = clientId,
            ClientName = string.IsNullOrWhiteSpace(clientMessage.SenderDisplayName) ? "Cliente Telegram" : clientMessage.SenderDisplayName.Trim(),
            ClientEmail = clientEmail ?? string.Empty,
            ServiceRequestId = triageState.ServiceRequestId,
            ServiceCategory = FirstNonEmpty(triageState.CategoryRaw, triageState.CategoryEnum),
            PostalCode = triageState.ZipCode ?? string.Empty,
            City = triageState.City ?? string.Empty,
            StatusNote = TrimToLimit(string.IsNullOrWhiteSpace(protocol)
                ? description
                : $"{description} Pedido #{protocol}.", 500) ?? "Lead originado automaticamente pelo bot Telegram.",
            InternalNotes = TrimToLimit(internalNotes.Trim(), 4000) ?? string.Empty,
            LastContactAtUtc = clientMessage.SentAtUtc.UtcDateTime
        };
    }

    private async Task<TelegramChatbotAssistantReply> ApplyNaturalQueryFlowAsync(
        string apiToken,
        Guid conversationId,
        TelegramChatbotConversationHistoryDto? history,
        ChatMessageDto clientMessage,
        TelegramChatbotAssistantReply reply,
        CancellationToken cancellationToken)
    {
        var referenceState = TryReadLatestQueryReferenceState(history);
        var decision = ResolveQueryIntentDecision(reply, clientMessage.Text, referenceState);
        if (decision is null)
        {
            return reply;
        }

        _observabilityService.RecordBusinessEvent("query_request", success: true);

        return decision.IntentName switch
        {
            ListOrdersIntent => await HandleListOrdersIntentAsync(
                apiToken,
                conversationId,
                reply,
                decision,
                referenceState,
                cancellationToken),
            ListAppointmentsIntent => await HandleListAppointmentsIntentAsync(
                apiToken,
                conversationId,
                reply,
                decision,
                referenceState,
                cancellationToken),
            GetOrderStatusIntent => await HandleOrderStatusIntentAsync(
                apiToken,
                conversationId,
                reply,
                decision,
                referenceState,
                cancellationToken),
            GetOrderDetailsIntent => await HandleOrderDetailsIntentAsync(
                apiToken,
                conversationId,
                reply,
                decision,
                referenceState,
                cancellationToken),
            _ => reply
        };
    }

    private async Task<TelegramChatbotAssistantReply> HandleListOrdersIntentAsync(
        string apiToken,
        Guid conversationId,
        TelegramChatbotAssistantReply reply,
        QueryIntentDecision decision,
        QueryReferenceState? referenceState,
        CancellationToken cancellationToken)
    {
        var dependencyStart = Stopwatch.StartNew();
        var ordersResult = await _telegramChatbotApiClient.GetClientOrdersAsync(
            apiToken,
            decision.Skip,
            decision.Take,
            cancellationToken);
        dependencyStart.Stop();
        _observabilityService.RecordDependency(
            dependency: "api.telegram_chatbot.orders",
            success: ordersResult?.Success == true,
            latencyMilliseconds: dependencyStart.ElapsedMilliseconds,
            errorCode: ordersResult?.Success == true ? null : ordersResult?.ErrorCode ?? "query_orders_failed");

        if (ordersResult is null || !ordersResult.Success)
        {
            _observabilityService.RecordIncident(
                stage: "query_list_orders",
                errorCode: ordersResult?.ErrorCode ?? "query_orders_failed",
                correlationId: reply.CorrelationId,
                message: ordersResult?.ErrorMessage ?? "Falha ao consultar pedidos.");

            await PersistQueryActionAsync(
                apiToken,
                conversationId,
                actionType: "query_list_orders",
                status: ActionStatusFailed,
                reply,
                payloadJson: BuildQueryPayloadJson(decision),
                resultJson: null,
                errorCode: ordersResult?.ErrorCode ?? "query_orders_failed",
                errorMessage: ordersResult?.ErrorMessage ?? "Nao foi possivel consultar os pedidos agora.",
                lastStep: "query_orders_failed",
                cancellationToken);

            return reply with
            {
                Intent = ListOrdersIntent,
                NextStep = "query_orders_failed",
                MessageText = "Tive instabilidade para consultar seus pedidos agora. Se quiser, tento novamente em seguida.",
                EntitiesJson = BuildQueryEntitiesJson(decision.IntentName, null, null, decision.Skip, decision.Take)
            };
        }

        var orderReferences = ordersResult.Orders
            .Select(item => new QueryOrderReference(item.ServiceRequestId, item.Protocol))
            .ToList();

        var updatedState = BuildQueryReferenceState(
            previous: referenceState,
            intentName: ListOrdersIntent,
            currentServiceRequestId: orderReferences.FirstOrDefault()?.ServiceRequestId,
            currentProtocol: orderReferences.FirstOrDefault()?.Protocol,
            listedOrders: orderReferences,
            ordersSkip: ordersResult.Skip,
            ordersTake: ordersResult.Take,
            appointmentsSkip: referenceState?.LastAppointmentsSkip ?? 0,
            appointmentsTake: referenceState?.LastAppointmentsTake ?? 3,
            fromUtc: referenceState?.LastFromUtc,
            toUtc: referenceState?.LastToUtc);

        await PersistQueryContextSnapshotAsync(
            apiToken,
            conversationId,
            snapshotType: "query_intent_result",
            contextJson: BuildQueryResultSnapshotJson(decision.IntentName, decision, ordersResult, null, null, null),
            reply,
            lastStep: ordersResult.Orders.Count == 0 ? "query_orders_empty" : "query_orders_listed",
            cancellationToken);

        await PersistQueryReferenceStateAsync(
            apiToken,
            conversationId,
            updatedState,
            reply,
            cancellationToken);

        await PersistQueryActionAsync(
            apiToken,
            conversationId,
            actionType: "query_list_orders",
            status: ActionStatusSucceeded,
            reply,
            payloadJson: BuildQueryPayloadJson(decision),
            resultJson: BuildQueryResultJson(ordersResult),
            errorCode: null,
            errorMessage: null,
            lastStep: ordersResult.Orders.Count == 0 ? "query_orders_empty" : "query_orders_listed",
            cancellationToken);

        return reply with
        {
            Intent = ListOrdersIntent,
            NextStep = ordersResult.Orders.Count == 0 ? "query_orders_empty" : "query_orders_listed",
            MessageText = BuildOrdersQueryMessage(ordersResult),
            EntitiesJson = BuildQueryEntitiesJson(
                decision.IntentName,
                orderReferences,
                null,
                ordersResult.Skip,
                ordersResult.Take)
        };
    }

    private async Task<TelegramChatbotAssistantReply> HandleListAppointmentsIntentAsync(
        string apiToken,
        Guid conversationId,
        TelegramChatbotAssistantReply reply,
        QueryIntentDecision decision,
        QueryReferenceState? referenceState,
        CancellationToken cancellationToken)
    {
        var dependencyStart = Stopwatch.StartNew();
        var appointmentsResult = await _telegramChatbotApiClient.GetClientAppointmentsAsync(
            apiToken,
            decision.FromUtc,
            decision.ToUtc,
            decision.Skip,
            decision.Take,
            cancellationToken);
        dependencyStart.Stop();
        _observabilityService.RecordDependency(
            dependency: "api.telegram_chatbot.appointments",
            success: appointmentsResult?.Success == true,
            latencyMilliseconds: dependencyStart.ElapsedMilliseconds,
            errorCode: appointmentsResult?.Success == true ? null : appointmentsResult?.ErrorCode ?? "query_appointments_failed");

        if (appointmentsResult is null || !appointmentsResult.Success)
        {
            _observabilityService.RecordIncident(
                stage: "query_list_appointments",
                errorCode: appointmentsResult?.ErrorCode ?? "query_appointments_failed",
                correlationId: reply.CorrelationId,
                message: appointmentsResult?.ErrorMessage ?? "Falha ao consultar agenda.");

            await PersistQueryActionAsync(
                apiToken,
                conversationId,
                actionType: "query_list_appointments",
                status: ActionStatusFailed,
                reply,
                payloadJson: BuildQueryPayloadJson(decision),
                resultJson: null,
                errorCode: appointmentsResult?.ErrorCode ?? "query_appointments_failed",
                errorMessage: appointmentsResult?.ErrorMessage ?? "Nao foi possivel consultar sua agenda agora.",
                lastStep: "query_appointments_failed",
                cancellationToken);

            return reply with
            {
                Intent = ListAppointmentsIntent,
                NextStep = "query_appointments_failed",
                MessageText = "Tive instabilidade para consultar seus agendamentos agora. Posso tentar de novo em seguida.",
                EntitiesJson = BuildQueryEntitiesJson(decision.IntentName, null, null, decision.Skip, decision.Take)
            };
        }

        var orderReferences = appointmentsResult.Appointments
            .Select(item => new QueryOrderReference(item.ServiceRequestId, item.Protocol))
            .DistinctBy(item => item.ServiceRequestId)
            .ToList();

        var updatedState = BuildQueryReferenceState(
            previous: referenceState,
            intentName: ListAppointmentsIntent,
            currentServiceRequestId: orderReferences.FirstOrDefault()?.ServiceRequestId,
            currentProtocol: orderReferences.FirstOrDefault()?.Protocol,
            listedOrders: orderReferences,
            ordersSkip: referenceState?.LastOrdersSkip ?? 0,
            ordersTake: referenceState?.LastOrdersTake ?? 3,
            appointmentsSkip: appointmentsResult.Skip,
            appointmentsTake: appointmentsResult.Take,
            fromUtc: decision.FromUtc,
            toUtc: decision.ToUtc);

        await PersistQueryContextSnapshotAsync(
            apiToken,
            conversationId,
            snapshotType: "query_intent_result",
            contextJson: BuildQueryResultSnapshotJson(decision.IntentName, decision, null, null, null, appointmentsResult),
            reply,
            lastStep: appointmentsResult.Appointments.Count == 0 ? "query_appointments_empty" : "query_appointments_listed",
            cancellationToken);

        await PersistQueryReferenceStateAsync(
            apiToken,
            conversationId,
            updatedState,
            reply,
            cancellationToken);

        await PersistQueryActionAsync(
            apiToken,
            conversationId,
            actionType: "query_list_appointments",
            status: ActionStatusSucceeded,
            reply,
            payloadJson: BuildQueryPayloadJson(decision),
            resultJson: BuildQueryResultJson(appointmentsResult),
            errorCode: null,
            errorMessage: null,
            lastStep: appointmentsResult.Appointments.Count == 0 ? "query_appointments_empty" : "query_appointments_listed",
            cancellationToken);

        return reply with
        {
            Intent = ListAppointmentsIntent,
            NextStep = appointmentsResult.Appointments.Count == 0 ? "query_appointments_empty" : "query_appointments_listed",
            MessageText = BuildAppointmentsQueryMessage(appointmentsResult),
            EntitiesJson = BuildQueryEntitiesJson(
                decision.IntentName,
                orderReferences,
                null,
                appointmentsResult.Skip,
                appointmentsResult.Take)
        };
    }

    private async Task<TelegramChatbotAssistantReply> HandleOrderStatusIntentAsync(
        string apiToken,
        Guid conversationId,
        TelegramChatbotAssistantReply reply,
        QueryIntentDecision decision,
        QueryReferenceState? referenceState,
        CancellationToken cancellationToken)
    {
        var targetServiceRequestId = await ResolveServiceRequestIdForQueryAsync(
            apiToken,
            decision,
            referenceState,
            cancellationToken);

        if (!targetServiceRequestId.HasValue)
        {
            return reply with
            {
                Intent = GetOrderStatusIntent,
                NextStep = "query_request_reference_missing",
                MessageText = "Nao encontrei um pedido de referencia. Se quiser, eu listo seus pedidos para voce escolher um protocolo.",
                EntitiesJson = BuildQueryEntitiesJson(decision.IntentName, null, null, decision.Skip, decision.Take)
            };
        }

        var dependencyStart = Stopwatch.StartNew();
        var statusResult = await _telegramChatbotApiClient.GetOrderStatusAsync(
            apiToken,
            targetServiceRequestId.Value,
            cancellationToken);
        dependencyStart.Stop();
        _observabilityService.RecordDependency(
            dependency: "api.telegram_chatbot.order_status",
            success: statusResult?.Success == true,
            latencyMilliseconds: dependencyStart.ElapsedMilliseconds,
            errorCode: statusResult?.Success == true ? null : statusResult?.ErrorCode ?? "query_order_status_failed");

        if (statusResult is null || !statusResult.Success)
        {
            _observabilityService.RecordIncident(
                stage: "query_get_order_status",
                errorCode: statusResult?.ErrorCode ?? "query_order_status_failed",
                correlationId: reply.CorrelationId,
                message: statusResult?.ErrorMessage ?? "Falha ao consultar status de pedido.");

            await PersistQueryActionAsync(
                apiToken,
                conversationId,
                actionType: "query_get_order_status",
                status: ActionStatusFailed,
                reply,
                payloadJson: BuildQueryPayloadJson(decision),
                resultJson: null,
                errorCode: statusResult?.ErrorCode ?? "query_order_status_failed",
                errorMessage: statusResult?.ErrorMessage ?? "Nao foi possivel consultar o status do pedido agora.",
                lastStep: "query_order_status_failed",
                cancellationToken);

            return reply with
            {
                Intent = GetOrderStatusIntent,
                NextStep = "query_order_status_failed",
                MessageText = "Nao consegui consultar o status desse pedido agora. Me pede novamente em instantes.",
                EntitiesJson = BuildQueryEntitiesJson(decision.IntentName, null, targetServiceRequestId, decision.Skip, decision.Take)
            };
        }

        var updatedState = BuildQueryReferenceState(
            previous: referenceState,
            intentName: GetOrderStatusIntent,
            currentServiceRequestId: statusResult.ServiceRequestId,
            currentProtocol: statusResult.Protocol,
            listedOrders: referenceState?.LastListedOrders ?? [],
            ordersSkip: referenceState?.LastOrdersSkip ?? 0,
            ordersTake: referenceState?.LastOrdersTake ?? 3,
            appointmentsSkip: referenceState?.LastAppointmentsSkip ?? 0,
            appointmentsTake: referenceState?.LastAppointmentsTake ?? 3,
            fromUtc: referenceState?.LastFromUtc,
            toUtc: referenceState?.LastToUtc);

        await PersistQueryContextSnapshotAsync(
            apiToken,
            conversationId,
            snapshotType: "query_intent_result",
            contextJson: BuildQueryResultSnapshotJson(decision.IntentName, decision, null, statusResult, null, null),
            reply,
            lastStep: "query_order_status_returned",
            cancellationToken);

        await PersistQueryReferenceStateAsync(
            apiToken,
            conversationId,
            updatedState,
            reply,
            cancellationToken);

        await PersistQueryActionAsync(
            apiToken,
            conversationId,
            actionType: "query_get_order_status",
            status: ActionStatusSucceeded,
            reply,
            payloadJson: BuildQueryPayloadJson(decision),
            resultJson: BuildQueryResultJson(statusResult),
            errorCode: null,
            errorMessage: null,
            lastStep: "query_order_status_returned",
            cancellationToken);

        return reply with
        {
            Intent = GetOrderStatusIntent,
            NextStep = "query_order_status_returned",
            MessageText = BuildOrderStatusQueryMessage(statusResult),
            EntitiesJson = BuildQueryEntitiesJson(decision.IntentName, null, statusResult.ServiceRequestId, decision.Skip, decision.Take)
        };
    }

    private async Task<TelegramChatbotAssistantReply> HandleOrderDetailsIntentAsync(
        string apiToken,
        Guid conversationId,
        TelegramChatbotAssistantReply reply,
        QueryIntentDecision decision,
        QueryReferenceState? referenceState,
        CancellationToken cancellationToken)
    {
        var targetServiceRequestId = await ResolveServiceRequestIdForQueryAsync(
            apiToken,
            decision,
            referenceState,
            cancellationToken);

        if (!targetServiceRequestId.HasValue)
        {
            return reply with
            {
                Intent = GetOrderDetailsIntent,
                NextStep = "query_request_reference_missing",
                MessageText = "Nao consegui identificar qual pedido voce quer detalhar. Se quiser, eu listo seus pedidos para voce escolher um protocolo.",
                EntitiesJson = BuildQueryEntitiesJson(decision.IntentName, null, null, decision.Skip, decision.Take)
            };
        }

        var dependencyStart = Stopwatch.StartNew();
        var detailsResult = await _telegramChatbotApiClient.GetOrderDetailsAsync(
            apiToken,
            targetServiceRequestId.Value,
            cancellationToken);
        dependencyStart.Stop();
        _observabilityService.RecordDependency(
            dependency: "api.telegram_chatbot.order_details",
            success: detailsResult?.Success == true && detailsResult.Details is not null,
            latencyMilliseconds: dependencyStart.ElapsedMilliseconds,
            errorCode: detailsResult?.Success == true && detailsResult.Details is not null
                ? null
                : detailsResult?.ErrorCode ?? "query_order_details_failed");

        if (detailsResult is null || !detailsResult.Success || detailsResult.Details is null)
        {
            _observabilityService.RecordIncident(
                stage: "query_get_order_details",
                errorCode: detailsResult?.ErrorCode ?? "query_order_details_failed",
                correlationId: reply.CorrelationId,
                message: detailsResult?.ErrorMessage ?? "Falha ao consultar detalhes do pedido.");

            await PersistQueryActionAsync(
                apiToken,
                conversationId,
                actionType: "query_get_order_details",
                status: ActionStatusFailed,
                reply,
                payloadJson: BuildQueryPayloadJson(decision),
                resultJson: null,
                errorCode: detailsResult?.ErrorCode ?? "query_order_details_failed",
                errorMessage: detailsResult?.ErrorMessage ?? "Nao foi possivel consultar os detalhes desse pedido.",
                lastStep: "query_order_details_failed",
                cancellationToken);

            return reply with
            {
                Intent = GetOrderDetailsIntent,
                NextStep = "query_order_details_failed",
                MessageText = "Nao consegui abrir os detalhes desse pedido agora. Se quiser, tento novamente em seguida.",
                EntitiesJson = BuildQueryEntitiesJson(decision.IntentName, null, targetServiceRequestId, decision.Skip, decision.Take)
            };
        }

        var detailsState = BuildQueryReferenceState(
            previous: referenceState,
            intentName: GetOrderDetailsIntent,
            currentServiceRequestId: detailsResult.ServiceRequestId,
            currentProtocol: detailsResult.Details.Protocol,
            listedOrders: referenceState?.LastListedOrders ?? [],
            ordersSkip: referenceState?.LastOrdersSkip ?? 0,
            ordersTake: referenceState?.LastOrdersTake ?? 3,
            appointmentsSkip: referenceState?.LastAppointmentsSkip ?? 0,
            appointmentsTake: referenceState?.LastAppointmentsTake ?? 3,
            fromUtc: referenceState?.LastFromUtc,
            toUtc: referenceState?.LastToUtc);

        await PersistQueryContextSnapshotAsync(
            apiToken,
            conversationId,
            snapshotType: "query_intent_result",
            contextJson: BuildQueryResultSnapshotJson(decision.IntentName, decision, null, null, detailsResult, null),
            reply,
            lastStep: "query_order_details_returned",
            cancellationToken);

        await PersistQueryReferenceStateAsync(
            apiToken,
            conversationId,
            detailsState,
            reply,
            cancellationToken);

        await PersistQueryActionAsync(
            apiToken,
            conversationId,
            actionType: "query_get_order_details",
            status: ActionStatusSucceeded,
            reply,
            payloadJson: BuildQueryPayloadJson(decision),
            resultJson: BuildQueryResultJson(detailsResult),
            errorCode: null,
            errorMessage: null,
            lastStep: "query_order_details_returned",
            cancellationToken);

        return reply with
        {
            Intent = GetOrderDetailsIntent,
            NextStep = "query_order_details_returned",
            MessageText = BuildOrderDetailsQueryMessage(detailsResult.Details),
            EntitiesJson = BuildQueryEntitiesJson(decision.IntentName, null, detailsResult.ServiceRequestId, decision.Skip, decision.Take)
        };
    }

    private async Task<Guid?> ResolveServiceRequestIdForQueryAsync(
        string apiToken,
        QueryIntentDecision decision,
        QueryReferenceState? referenceState,
        CancellationToken cancellationToken)
    {
        if (decision.ServiceRequestId.HasValue)
        {
            return decision.ServiceRequestId.Value;
        }

        if (!string.IsNullOrWhiteSpace(decision.Protocol))
        {
            var fromReference = referenceState?.LastListedOrders
                .FirstOrDefault(item => item.Protocol.Equals(decision.Protocol, StringComparison.OrdinalIgnoreCase));
            if (fromReference is not null)
            {
                return fromReference.ServiceRequestId;
            }

            var dependencyStart = Stopwatch.StartNew();
            var orders = await _telegramChatbotApiClient.GetClientOrdersAsync(
                apiToken,
                skip: 0,
                take: 20,
                cancellationToken);
            dependencyStart.Stop();
            _observabilityService.RecordDependency(
                dependency: "api.telegram_chatbot.orders",
                success: orders?.Success == true,
                latencyMilliseconds: dependencyStart.ElapsedMilliseconds,
                errorCode: orders?.Success == true ? null : orders?.ErrorCode ?? "query_orders_failed");
            if (orders?.Success == true)
            {
                var resolved = orders.Orders.FirstOrDefault(item =>
                    item.Protocol.Equals(decision.Protocol, StringComparison.OrdinalIgnoreCase));
                if (resolved is not null)
                {
                    return resolved.ServiceRequestId;
                }
            }
        }

        if (referenceState?.CurrentServiceRequestId.HasValue == true)
        {
            return referenceState.CurrentServiceRequestId.Value;
        }

        var fallbackDependencyStart = Stopwatch.StartNew();
        var fallbackOrders = await _telegramChatbotApiClient.GetClientOrdersAsync(
            apiToken,
            skip: 0,
            take: 1,
            cancellationToken);
        fallbackDependencyStart.Stop();
        _observabilityService.RecordDependency(
            dependency: "api.telegram_chatbot.orders",
            success: fallbackOrders?.Success == true,
            latencyMilliseconds: fallbackDependencyStart.ElapsedMilliseconds,
            errorCode: fallbackOrders?.Success == true ? null : fallbackOrders?.ErrorCode ?? "query_orders_failed");

        if (fallbackOrders?.Success == true && fallbackOrders.Orders.Count > 0)
        {
            return fallbackOrders.Orders[0].ServiceRequestId;
        }

        return null;
    }

    private async Task<TelegramChatbotAssistantReply> ApplySchedulingFlowAsync(
        string apiToken,
        Guid conversationId,
        TelegramChatbotConversationHistoryDto? history,
        ChatMessageDto clientMessage,
        TelegramChatbotAssistantReply reply,
        CancellationToken cancellationToken)
    {
        if (IsQueryIntent(reply.Intent))
        {
            return reply;
        }

        var serviceRequestId = TryExtractServiceRequestId(reply.EntitiesJson)
            ?? TryExtractServiceRequestIdFromHistory(history);
        if (!serviceRequestId.HasValue)
        {
            return reply;
        }

        var isStatusQuery = IsSchedulingStatusQuery(clientMessage.Text);
        var isProviderQuery = IsProviderQuery(clientMessage.Text);

        var shouldSuggestProviders =
            reply.NextStep.Equals("service_request_created", StringComparison.OrdinalIgnoreCase);

        var parseResult = _telegramSchedulingNaturalLanguageParser.Parse(clientMessage.Text, DateTime.UtcNow);

        var latestBatchResult = TryReadLatestSchedulingBatchResult(history, serviceRequestId.Value);

        if (isStatusQuery && latestBatchResult is not null)
        {
            return reply with
            {
                Intent = ScheduleVisitsIntent,
                NextStep = "schedule_status",
                MessageText = BuildSchedulingStatusMessage(latestBatchResult),
                EntitiesJson = BuildSchedulingEntitiesJson(serviceRequestId.Value, [], parseResult, latestBatchResult)
            };
        }

        if (shouldSuggestProviders && !parseResult.IsSchedulingIntent)
        {
            return reply with
            {
                Intent = ScheduleVisitsIntent,
                NextStep = "collect_visit_windows",
                MessageText = BuildVisitWindowQuestionMessage(),
                EntitiesJson = BuildSchedulingEntitiesJson(serviceRequestId.Value, [], parseResult, null)
            };
        }

        if (parseResult.ErrorCode is not null && !isStatusQuery)
        {
            return reply with
            {
                Intent = ScheduleVisitsIntent,
                NextStep = parseResult.ErrorCode,
                MessageText = parseResult.ErrorMessage ?? BuildVisitWindowQuestionMessage(),
                EntitiesJson = BuildSchedulingEntitiesJson(serviceRequestId.Value, [], parseResult, null)
            };
        }

        var shouldLookupProviders = parseResult.IsSchedulingIntent || isStatusQuery || isProviderQuery;
        if (!shouldLookupProviders)
        {
            return ApplySchedulingPersistenceGuardrail(
                reply,
                serviceRequestId.Value,
                parseResult,
                latestBatchResult);
        }

        var providerLookupStart = Stopwatch.StartNew();
        var providersResult = await _telegramChatbotApiClient.GetEligibleProvidersAsync(
            apiToken,
            serviceRequestId.Value,
            take: 5,
            cancellationToken);
        providerLookupStart.Stop();
        _observabilityService.RecordDependency(
            dependency: "api.telegram_chatbot.eligible_providers",
            success: providersResult?.Success == true,
            latencyMilliseconds: providerLookupStart.ElapsedMilliseconds,
            errorCode: providersResult?.Success == true ? null : providersResult?.ErrorCode ?? "provider_lookup_failed");

        if (providersResult is null || !providersResult.Success)
        {
            _observabilityService.RecordIncident(
                stage: "schedule_provider_lookup",
                errorCode: providersResult?.ErrorCode ?? "provider_lookup_failed",
                correlationId: reply.CorrelationId,
                message: providersResult?.ErrorMessage ?? "Falha ao consultar prestadores elegiveis.");

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

            return ApplySchedulingPersistenceGuardrail(
                reply,
                serviceRequestId.Value,
                parseResult,
                latestBatchResult);
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

        if (isStatusQuery && latestBatchResult is null)
        {
            return reply with
            {
                Intent = ScheduleVisitsIntent,
                NextStep = "schedule_status_unavailable",
                MessageText = $"Ainda nao tenho visitas agendadas para esse pedido.\n\n{BuildProviderSuggestionMessage(providers)}",
                EntitiesJson = BuildSchedulingEntitiesJson(serviceRequestId.Value, providers, parseResult, null)
            };
        }

        if (isProviderQuery && parseResult.ErrorCode is not null)
        {
            return reply with
            {
                Intent = ScheduleVisitsIntent,
                NextStep = "providers_listed",
                MessageText = BuildProviderSuggestionMessage(providers),
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

        var providerAssignments = await ResolveProviderAssignmentsAsync(
            apiToken,
            providers,
            parseResult.Windows.Take(visitCount).ToList(),
            cancellationToken);

        if (providerAssignments.Count == 0)
        {
            return reply with
            {
                Intent = ScheduleVisitsIntent,
                NextStep = "no_provider_slot_for_windows",
                MessageText = "Nao encontrei janelas livres desses prestadores para os dias/periodos informados. Me diga outros dias/periodos e eu tento novamente.",
                EntitiesJson = BuildSchedulingEntitiesJson(serviceRequestId.Value, providers, parseResult, null)
            };
        }

        if (providerAssignments.Count < visitCount)
        {
            return reply with
            {
                Intent = ScheduleVisitsIntent,
                NextStep = "partial_provider_slot_for_windows",
                MessageText = $"Encontrei disponibilidade para {providerAssignments.Count} de {visitCount} visita(s) nessas janelas. Se quiser manter as 3 visitas, me passe outros dias/periodos.",
                EntitiesJson = BuildSchedulingEntitiesJson(serviceRequestId.Value, providers, parseResult, null)
            };
        }

        var visitRequests = new List<TelegramChatbotBatchScheduleVisitRequestDto>(visitCount);
        foreach (var assignment in providerAssignments)
        {
            visitRequests.Add(new TelegramChatbotBatchScheduleVisitRequestDto(
                ProviderId: assignment.Provider.ProviderId,
                WindowStartUtc: assignment.Window.WindowStartUtc,
                WindowEndUtc: assignment.Window.WindowEndUtc,
                Reason: "Agendamento solicitado em linguagem natural pelo chatbot Telegram."));
        }

        var scheduleBatchStart = Stopwatch.StartNew();
        var batchResult = await _telegramChatbotApiClient.ScheduleVisitsBatchAsync(
            apiToken,
            serviceRequestId.Value,
            visitRequests,
            cancellationToken);
        scheduleBatchStart.Stop();
        _observabilityService.RecordDependency(
            dependency: "api.telegram_chatbot.schedule_batch",
            success: batchResult is not null && batchResult.Success,
            latencyMilliseconds: scheduleBatchStart.ElapsedMilliseconds,
            errorCode: batchResult is not null && batchResult.Success
                ? null
                : batchResult?.ErrorCode ?? "schedule_batch_failed");

        if (batchResult is null)
        {
            _observabilityService.RecordBusinessEvent("scheduling_attempt", success: false);
            _observabilityService.RecordIncident(
                stage: "schedule_batch_create",
                errorCode: "schedule_batch_failed",
                correlationId: reply.CorrelationId,
                message: "POST /schedule-visits-batch retornou vazio.");

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

        _observabilityService.RecordBusinessEvent("scheduling_attempt", success: batchResult.Success);
        if (!batchResult.Success)
        {
            _observabilityService.RecordIncident(
                stage: "schedule_batch_create",
                errorCode: batchResult.ErrorCode ?? "schedule_batch_failed",
                correlationId: reply.CorrelationId,
                message: batchResult.ErrorMessage);
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

    private async Task<IReadOnlyList<ProviderWindowAssignment>> ResolveProviderAssignmentsAsync(
        string apiToken,
        IReadOnlyList<TelegramChatbotEligibleProviderDto> providers,
        IReadOnlyList<TelegramSchedulingParseVisitWindow> windows,
        CancellationToken cancellationToken)
    {
        var assignments = new List<ProviderWindowAssignment>(windows.Count);
        var usedProviders = new HashSet<Guid>();

        foreach (var window in windows)
        {
            TelegramChatbotEligibleProviderDto? selectedProvider = null;
            foreach (var provider in providers)
            {
                if (usedProviders.Contains(provider.ProviderId))
                {
                    continue;
                }

                if (await IsProviderAvailableForWindowAsync(
                        apiToken,
                        provider.ProviderId,
                        window.WindowStartUtc,
                        window.WindowEndUtc,
                        cancellationToken))
                {
                    selectedProvider = provider;
                    break;
                }
            }

            if (selectedProvider is null)
            {
                continue;
            }

            usedProviders.Add(selectedProvider.ProviderId);
            assignments.Add(new ProviderWindowAssignment(selectedProvider, window));
        }

        return assignments;
    }

    private async Task<bool> IsProviderAvailableForWindowAsync(
        string apiToken,
        Guid providerId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        CancellationToken cancellationToken)
    {
        var durationMinutes = (int)Math.Round(
            (NormalizeToUtc(windowEndUtc) - NormalizeToUtc(windowStartUtc)).TotalMinutes,
            MidpointRounding.AwayFromZero);

        if (durationMinutes <= 0)
        {
            return false;
        }

        var slotsLookupStart = Stopwatch.StartNew();
        var slots = await _telegramChatbotApiClient.GetProviderAvailableSlotsAsync(
            apiToken,
            providerId,
            windowStartUtc,
            windowEndUtc,
            durationMinutes,
            cancellationToken);
        slotsLookupStart.Stop();
        _observabilityService.RecordDependency(
            dependency: "api.service_appointments.slots",
            success: slots is not null,
            latencyMilliseconds: slotsLookupStart.ElapsedMilliseconds,
            errorCode: slots is not null ? null : "slots_lookup_failed");

        if (slots is null || slots.Count == 0)
        {
            return false;
        }

        var normalizedStart = NormalizeToUtc(windowStartUtc);
        var normalizedEnd = NormalizeToUtc(windowEndUtc);

        return slots.Any(slot =>
            NormalizeToUtc(slot.WindowStartUtc) <= normalizedStart &&
            NormalizeToUtc(slot.WindowEndUtc) >= normalizedEnd);
    }

    private static string BuildVisitWindowQuestionMessage()
    {
        return "Perfeito. Para eu buscar prestadores com agenda livre, me diga os dias e o periodo desejado (ex.: quarta e sexta de manha).";
    }

    private static TelegramChatbotAssistantReply ApplySchedulingPersistenceGuardrail(
        TelegramChatbotAssistantReply reply,
        Guid serviceRequestId,
        TelegramSchedulingParseResult parseResult,
        TelegramChatbotBatchScheduleResultDto? latestBatchResult)
    {
        if (!IsSchedulingConfirmationClaim(reply.MessageText))
        {
            return reply;
        }

        var hasPersistedScheduling = latestBatchResult is not null &&
                                     latestBatchResult.Results.Any(item => item.Success);
        if (hasPersistedScheduling)
        {
            return reply;
        }

        return reply with
        {
            Intent = ScheduleVisitsIntent,
            NextStep = "awaiting_provider_confirmation",
            MessageText = BuildPendingProviderActionMessage(),
            EntitiesJson = BuildSchedulingEntitiesJson(serviceRequestId, [], parseResult, latestBatchResult)
        };
    }

    private static string BuildPendingProviderActionMessage()
    {
        return "O agendamento ainda nao foi confirmado no sistema. Ele precisa de uma acao do prestador para confirmar. Assim que tivermos a confirmacao, retorno com mais detalhes.";
    }

    private static string BuildFallbackEntitiesJson(string errorCode)
    {
        return TrimToLimit(
            JsonSerializer.Serialize(new
            {
                errorCode,
                fallback = true
            }, JsonOptions),
            MetadataPayloadLimit) ?? "{\"fallback\":true}";
    }

    private static string BuildGuardrailEntitiesJson(
        string ruleCode,
        string reason,
        string originalIntent,
        string originalNextStep)
    {
        return TrimToLimit(
            JsonSerializer.Serialize(new
            {
                guardrailRule = ruleCode,
                reason,
                originalIntent,
                originalNextStep
            }, JsonOptions),
            MetadataPayloadLimit) ?? "{\"guardrailRule\":\"unknown\"}";
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

    private static string BuildSchedulingStatusMessage(TelegramChatbotBatchScheduleResultDto batchResult)
    {
        var successCount = batchResult.Results.Count(item => item.Success);
        var failureCount = batchResult.Results.Count - successCount;

        if (successCount == 0)
        {
            return "Ainda nao tenho visitas confirmadas para esse pedido. Se quiser, me diga novos dias/periodos para tentar o agendamento.";
        }

        if (failureCount == 0)
        {
            return $"Sim. Ja temos {successCount} visita(s) solicitadas para esse pedido. Se quiser, posso te mostrar novas opcoes de horario.";
        }

        return $"Temos {successCount} visita(s) solicitadas e {failureCount} tentativa(s) pendente(s) por indisponibilidade. Posso tentar novos dias/periodos agora.";
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

    private QueryIntentDecision? ResolveQueryIntentDecision(
        TelegramChatbotAssistantReply reply,
        string? clientMessage,
        QueryReferenceState? referenceState)
    {
        var normalizedMessage = NormalizeMessage(clientMessage);
        var normalizedIntent = NormalizeIntentName(reply.Intent);
        var serviceRequestId = TryExtractServiceRequestId(reply.EntitiesJson);
        var protocol = ExtractProtocolFromMessage(clientMessage) ?? TryExtractProtocol(reply.EntitiesJson);

        var defaultTake = ResolveQueryTake(reply.EntitiesJson, normalizedMessage);
        var isMoreRequest = ContainsAny(normalizedMessage, "mais", "proxim");

        if (isMoreRequest && referenceState is not null)
        {
            if (referenceState.LastQueryIntent == ListOrdersIntent)
            {
                return new QueryIntentDecision(
                    IntentName: ListOrdersIntent,
                    ServiceRequestId: null,
                    Protocol: null,
                    Skip: referenceState.LastOrdersSkip + referenceState.LastOrdersTake,
                    Take: referenceState.LastOrdersTake,
                    FromUtc: null,
                    ToUtc: null);
            }

            if (referenceState.LastQueryIntent == ListAppointmentsIntent)
            {
                return new QueryIntentDecision(
                    IntentName: ListAppointmentsIntent,
                    ServiceRequestId: null,
                    Protocol: null,
                    Skip: referenceState.LastAppointmentsSkip + referenceState.LastAppointmentsTake,
                    Take: referenceState.LastAppointmentsTake,
                    FromUtc: referenceState.LastFromUtc,
                    ToUtc: referenceState.LastToUtc);
            }
        }

        var (fromUtc, toUtc) = ResolveAppointmentRange(normalizedMessage);

        if (normalizedIntent == ListOrdersIntent)
        {
            return new QueryIntentDecision(ListOrdersIntent, serviceRequestId, protocol, 0, defaultTake, null, null);
        }

        if (normalizedIntent == GetOrderStatusIntent)
        {
            return new QueryIntentDecision(GetOrderStatusIntent, serviceRequestId, protocol, 0, defaultTake, null, null);
        }

        if (normalizedIntent == GetOrderDetailsIntent)
        {
            return new QueryIntentDecision(GetOrderDetailsIntent, serviceRequestId, protocol, 0, defaultTake, null, null);
        }

        if (normalizedIntent == ListAppointmentsIntent)
        {
            return new QueryIntentDecision(ListAppointmentsIntent, serviceRequestId, protocol, 0, defaultTake, fromUtc, toUtc);
        }

        var asksOrderList = ContainsAny(normalizedMessage,
            "meus pedidos",
            "quais pedidos",
            "listar pedidos",
            "mostrar pedidos",
            "pedidos tenho");

        var asksAppointmentList = ContainsAny(normalizedMessage,
            "meus agendamentos",
            "quais agendamentos",
            "listar agendamentos",
            "mostrar agendamentos",
            "minha agenda",
            "quais visitas",
            "visitas tenho");

        var asksOrderStatus = ContainsAny(normalizedMessage,
                                  "status do pedido",
                                  "situacao do pedido",
                                  "situacao do pedido",
                                  "andamento do pedido",
                                  "como esta meu pedido")
                              || (ContainsAny(normalizedMessage, "status", "andamento", "situacao") &&
                                  ContainsAny(normalizedMessage, "pedido", "protocolo"));

        var asksOrderDetails = ContainsAny(normalizedMessage,
                                   "detalhes do pedido",
                                   "detalhe do pedido",
                                   "mostrar detalhes",
                                   "me mostra detalhes",
                                   "detalhar pedido")
                               || (ContainsAny(normalizedMessage, "detalhe", "detalhes") &&
                                   (ContainsAny(normalizedMessage, "pedido", "protocolo") ||
                                    protocol is not null ||
                                    serviceRequestId.HasValue));

        if (asksOrderDetails)
        {
            return new QueryIntentDecision(GetOrderDetailsIntent, serviceRequestId, protocol, 0, defaultTake, null, null);
        }

        if (asksOrderStatus)
        {
            return new QueryIntentDecision(GetOrderStatusIntent, serviceRequestId, protocol, 0, defaultTake, null, null);
        }

        if (asksAppointmentList)
        {
            return new QueryIntentDecision(ListAppointmentsIntent, serviceRequestId, protocol, 0, defaultTake, fromUtc, toUtc);
        }

        if (asksOrderList)
        {
            return new QueryIntentDecision(ListOrdersIntent, serviceRequestId, protocol, 0, defaultTake, null, null);
        }

        return null;
    }

    private static string BuildOrdersQueryMessage(TelegramChatbotOrdersResultDto result)
    {
        if (result.TotalCount == 0)
        {
            return "Voce ainda nao tem pedidos registrados. Se quiser, posso te ajudar a abrir um novo pedido agora.";
        }

        if (result.Orders.Count == 0)
        {
            return "Nao encontrei mais pedidos nessa pagina. Se quiser, eu volto para os primeiros pedidos.";
        }

        var lines = result.Orders
            .Select(item =>
            {
                var nextVisit = item.NextAppointmentStartUtc.HasValue
                    ? $" | Proxima visita: {BuildWindowLabel(item.NextAppointmentStartUtc.Value, item.NextAppointmentEndUtc ?? item.NextAppointmentStartUtc.Value)}"
                    : string.Empty;

                return $"- #{item.Protocol} | {TranslateRequestStatus(item.Status)} | {item.Category} | {item.City}{nextVisit}";
            })
            .ToList();

        var message = "Encontrei estes pedidos para voce:\n" + string.Join("\n", lines);
        if (result.HasMore)
        {
            message += "\n\nSe quiser, me diga \"mostrar mais pedidos\" que eu trago a proxima pagina.";
        }

        return message;
    }

    private static string BuildAppointmentsQueryMessage(TelegramChatbotAppointmentsResultDto result)
    {
        if (result.TotalCount == 0)
        {
            return "No momento, voce nao tem agendamentos registrados. Quando houver visitas confirmadas, eu te aviso por aqui.";
        }

        if (result.Appointments.Count == 0)
        {
            return "Nao encontrei mais agendamentos nessa pagina. Se quiser, eu volto para os primeiros horarios.";
        }

        var lines = result.Appointments
            .Select(item =>
                $"- Pedido #{item.Protocol} | {item.ProviderName} | {TranslateAppointmentStatus(item.Status)} | {BuildWindowLabel(item.WindowStartUtc, item.WindowEndUtc)}")
            .ToList();

        var message = "Sua agenda atual:\n" + string.Join("\n", lines);
        if (result.HasMore)
        {
            message += "\n\nSe quiser, me diga \"mostrar mais agendamentos\" para trazer a proxima pagina.";
        }

        return message;
    }

    private static string BuildOrderStatusQueryMessage(TelegramChatbotOrderStatusResultDto result)
    {
        var summary = $"Pedido #{result.Protocol} esta em {TranslateRequestStatus(result.Status)}. " +
                      $"Propostas: {result.ProposalsCount} (aceitas: {result.AcceptedProposalsCount}). " +
                      $"Agendamentos: {result.AppointmentsCount}.";

        if (result.NextAppointment is null)
        {
            return summary + " Se quiser, eu te mostro os detalhes completos desse pedido.";
        }

        var nextVisit = result.NextAppointment;
        return summary + $" Proxima visita: {nextVisit.ProviderName}, {BuildWindowLabel(nextVisit.WindowStartUtc, nextVisit.WindowEndUtc)} ({TranslateAppointmentStatus(nextVisit.Status)}).";
    }

    private static string BuildOrderDetailsQueryMessage(TelegramChatbotOrderDetailsDto details)
    {
        var appointmentLine = details.Appointments.Count == 0
            ? "Sem visitas registradas."
            : $"Ultima visita: {details.Appointments[0].ProviderName} em {BuildWindowLabel(details.Appointments[0].WindowStartUtc, details.Appointments[0].WindowEndUtc)} ({TranslateAppointmentStatus(details.Appointments[0].Status)}).";

        var proposalLine = details.Proposals.Count == 0
            ? "Sem propostas registradas ainda."
            : $"Propostas: {details.Proposals.Count} (aceitas: {details.Proposals.Count(item => item.Accepted)}).";

        return $"Detalhes do pedido #{details.Protocol}: {TranslateRequestStatus(details.Status)} em {details.Category}. " +
               $"Descricao: {TrimToLimit(details.Description, 180)}. {proposalLine} {appointmentLine}";
    }

    private async Task PersistQueryReferenceStateAsync(
        string apiToken,
        Guid conversationId,
        QueryReferenceState state,
        TelegramChatbotAssistantReply reply,
        CancellationToken cancellationToken)
    {
        await PersistQueryContextSnapshotAsync(
            apiToken,
            conversationId,
            snapshotType: "query_reference_state",
            contextJson: TrimToLimit(JsonSerializer.Serialize(state, JsonOptions), ContextPayloadLimit) ?? "{}",
            reply,
            lastStep: "query_reference_state_updated",
            cancellationToken);
    }

    private async Task PersistQueryContextSnapshotAsync(
        string apiToken,
        Guid conversationId,
        string snapshotType,
        string contextJson,
        TelegramChatbotAssistantReply reply,
        string lastStep,
        CancellationToken cancellationToken)
    {
        await _telegramChatbotApiClient.RegisterContextSnapshotAsync(
            apiToken,
            conversationId,
            snapshotType: snapshotType,
            contextJson: contextJson,
            promptVersion: reply.PromptVersion,
            modelName: reply.ModelName,
            promptTokens: reply.PromptTokens,
            completionTokens: reply.CompletionTokens,
            totalTokens: reply.TotalTokens,
            intentName: reply.Intent,
            lastStep: lastStep,
            cancellationToken);
    }

    private async Task PersistQueryActionAsync(
        string apiToken,
        Guid conversationId,
        string actionType,
        int status,
        TelegramChatbotAssistantReply reply,
        string? payloadJson,
        string? resultJson,
        string? errorCode,
        string? errorMessage,
        string lastStep,
        CancellationToken cancellationToken)
    {
        await _telegramChatbotApiClient.RegisterActionAsync(
            apiToken,
            conversationId,
            actionType: actionType,
            status: status,
            intentName: reply.Intent,
            payloadJson: TrimToLimit(payloadJson, ContextPayloadLimit),
            resultJson: TrimToLimit(resultJson, ContextPayloadLimit),
            errorCode: errorCode,
            errorMessage: errorMessage,
            correlationId: reply.CorrelationId,
            metadataJson: TrimToLimit(JsonSerializer.Serialize(new
            {
                stage = "st_009",
                origin = "telegram_bridge"
            }, JsonOptions), MetadataPayloadLimit),
            lastStep: lastStep,
            conversationStatus: ConversationStatusActive,
            cancellationToken);
    }

    private static QueryReferenceState BuildQueryReferenceState(
        QueryReferenceState? previous,
        string intentName,
        Guid? currentServiceRequestId,
        string? currentProtocol,
        IReadOnlyList<QueryOrderReference> listedOrders,
        int ordersSkip,
        int ordersTake,
        int appointmentsSkip,
        int appointmentsTake,
        DateTime? fromUtc = null,
        DateTime? toUtc = null)
    {
        var normalizedOrders = listedOrders
            .Where(item => item.ServiceRequestId != Guid.Empty && !string.IsNullOrWhiteSpace(item.Protocol))
            .DistinctBy(item => item.ServiceRequestId)
            .Take(20)
            .ToList();

        return new QueryReferenceState(
            CurrentServiceRequestId: currentServiceRequestId ?? previous?.CurrentServiceRequestId,
            CurrentProtocol: string.IsNullOrWhiteSpace(currentProtocol) ? previous?.CurrentProtocol : currentProtocol,
            LastListedOrders: normalizedOrders.Count > 0 ? normalizedOrders : previous?.LastListedOrders ?? [],
            LastQueryIntent: intentName,
            LastOrdersSkip: ordersSkip,
            LastOrdersTake: Math.Clamp(ordersTake, 1, 20),
            LastAppointmentsSkip: appointmentsSkip,
            LastAppointmentsTake: Math.Clamp(appointmentsTake, 1, 20),
            LastFromUtc: fromUtc ?? previous?.LastFromUtc,
            LastToUtc: toUtc ?? previous?.LastToUtc,
            UpdatedAtUtc: DateTime.UtcNow);
    }

    private static QueryReferenceState? TryReadLatestQueryReferenceState(TelegramChatbotConversationHistoryDto? history)
    {
        if (history is null || history.ContextSnapshots.Count == 0)
        {
            return null;
        }

        var snapshot = history.ContextSnapshots
            .Where(item => item.SnapshotType.Equals("query_reference_state", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CapturedAtUtc)
            .FirstOrDefault();

        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.ContextJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<QueryReferenceState>(snapshot.ContextJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildQueryPayloadJson(QueryIntentDecision decision)
    {
        return JsonSerializer.Serialize(new
        {
            decision.IntentName,
            decision.ServiceRequestId,
            decision.Protocol,
            decision.Skip,
            decision.Take,
            decision.FromUtc,
            decision.ToUtc
        }, JsonOptions);
    }

    private static string BuildQueryResultSnapshotJson(
        string intentName,
        QueryIntentDecision decision,
        TelegramChatbotOrdersResultDto? ordersResult,
        TelegramChatbotOrderStatusResultDto? statusResult,
        TelegramChatbotOrderDetailsResultDto? detailsResult,
        TelegramChatbotAppointmentsResultDto? appointmentsResult)
    {
        return TrimToLimit(JsonSerializer.Serialize(new
        {
            capturedAtUtc = DateTime.UtcNow,
            intentName,
            decision,
            ordersResult,
            statusResult,
            detailsResult,
            appointmentsResult
        }, JsonOptions), ContextPayloadLimit) ?? "{}";
    }

    private static string BuildQueryResultJson(object result)
    {
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private static string BuildQueryEntitiesJson(
        string intentName,
        IReadOnlyList<QueryOrderReference>? orderReferences,
        Guid? serviceRequestId,
        int skip,
        int take)
    {
        var payload = new
        {
            intent = intentName,
            serviceRequestId,
            pagination = new
            {
                skip,
                take
            },
            references = orderReferences?.Select(item => new
            {
                item.ServiceRequestId,
                item.Protocol
            }).ToList() ?? []
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static bool IsQueryIntent(string? intentName)
    {
        var normalized = NormalizeIntentName(intentName);
        return normalized == ListOrdersIntent
               || normalized == GetOrderStatusIntent
               || normalized == GetOrderDetailsIntent
               || normalized == ListAppointmentsIntent;
    }

    private static string NormalizeIntentName(string? intentName)
    {
        if (string.IsNullOrWhiteSpace(intentName))
        {
            return string.Empty;
        }

        return intentName.Trim().ToLowerInvariant();
    }

    private static string? TryExtractProtocol(string? entitiesJson)
    {
        if (string.IsNullOrWhiteSpace(entitiesJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(entitiesJson);
            if (!TryGetPropertyIgnoreCase(document.RootElement, "protocol", out var protocolElement))
            {
                return null;
            }

            var raw = protocolElement.ValueKind switch
            {
                JsonValueKind.String => protocolElement.GetString(),
                JsonValueKind.Number => protocolElement.GetRawText(),
                _ => null
            };

            return NormalizeProtocol(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractProtocolFromMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var match = ProtocolRegex.Match(message);
        if (!match.Success)
        {
            return null;
        }

        return NormalizeProtocol(match.Groups["protocol"].Value);
    }

    private static string? NormalizeProtocol(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cleaned = new string(raw
            .Trim()
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToLowerInvariant();

        return cleaned.Length == 8
            ? cleaned
            : null;
    }

    private static int ResolveQueryTake(string? entitiesJson, string normalizedMessage)
    {
        var byEntities = TryReadEntityInt(entitiesJson, "take")
                         ?? TryReadEntityInt(entitiesJson, "pageSize");
        if (byEntities.HasValue)
        {
            return Math.Clamp(byEntities.Value, 1, 10);
        }

        if (ContainsAny(normalizedMessage, "todos", "todas"))
        {
            return 10;
        }

        return 3;
    }

    private static int? TryReadEntityInt(string? entitiesJson, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(entitiesJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(entitiesJson);
            if (!TryGetPropertyIgnoreCase(document.RootElement, fieldName, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
            {
                return numeric;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out numeric))
            {
                return numeric;
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (DateTime? fromUtc, DateTime? toUtc) ResolveAppointmentRange(string normalizedMessage)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BusinessTimeZone);
        if (ContainsAny(normalizedMessage, "hoje"))
        {
            var startLocal = nowLocal.Date;
            var endLocal = startLocal.AddDays(1).AddTicks(-1);
            return (ConvertLocalToUtc(startLocal), ConvertLocalToUtc(endLocal));
        }

        if (ContainsAny(normalizedMessage, "amanha"))
        {
            var startLocal = nowLocal.Date.AddDays(1);
            var endLocal = startLocal.AddDays(1).AddTicks(-1);
            return (ConvertLocalToUtc(startLocal), ConvertLocalToUtc(endLocal));
        }

        if (ContainsAny(normalizedMessage, "semana que vem", "semana q vem", "proxima semana"))
        {
            var startOfNextWeek = nowLocal.Date.AddDays(7 - (int)nowLocal.DayOfWeek + 1);
            var endOfNextWeek = startOfNextWeek.AddDays(7).AddTicks(-1);
            return (ConvertLocalToUtc(startOfNextWeek), ConvertLocalToUtc(endOfNextWeek));
        }

        return (null, null);
    }

    private static DateTime ConvertLocalToUtc(DateTime localDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified),
            BusinessTimeZone);
    }

    private static bool ContainsAny(string text, params string[] terms)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return terms.Any(term => text.Contains(term, StringComparison.Ordinal));
    }

    private static string TranslateRequestStatus(string status)
    {
        return status.Trim().ToLowerInvariant() switch
        {
            "created" => "criacao",
            "matching" => "busca de prestador",
            "scheduled" => "agendado",
            "inprogress" => "em andamento",
            "completed" => "concluido",
            "cancelled" => "cancelado",
            _ => status
        };
    }

    private static string TranslateAppointmentStatus(string status)
    {
        return status.Trim().ToLowerInvariant() switch
        {
            "pendingproviderconfirmation" => "aguardando confirmacao do prestador",
            "confirmed" => "confirmado",
            "rescheduleconfirmed" => "reagendado confirmado",
            "rejectedbyprovider" => "rejeitado pelo prestador",
            "cancelledbyclient" => "cancelado pelo cliente",
            "cancelledbyprovider" => "cancelado pelo prestador",
            "expiredwithoutprovideraction" => "expirado sem acao do prestador",
            "completed" => "concluido",
            _ => status
        };
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

    private static Guid? TryExtractServiceRequestIdFromHistory(TelegramChatbotConversationHistoryDto? history)
    {
        if (history is null || history.ContextSnapshots.Count == 0)
        {
            return null;
        }

        var latestStateSnapshot = history.ContextSnapshots
            .OrderByDescending(item => item.CapturedAtUtc)
            .FirstOrDefault(item => item.SnapshotType.Equals("service_request_triage_state", StringComparison.OrdinalIgnoreCase));

        if (latestStateSnapshot is null || string.IsNullOrWhiteSpace(latestStateSnapshot.ContextJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(latestStateSnapshot.ContextJson);
            if (TryGetPropertyIgnoreCase(document.RootElement, "state", out var stateElement))
            {
                if (TryGetPropertyIgnoreCase(stateElement, "serviceRequestId", out var requestIdElement))
                {
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
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TelegramChatbotBatchScheduleResultDto? TryReadLatestSchedulingBatchResult(
        TelegramChatbotConversationHistoryDto? history,
        Guid serviceRequestId)
    {
        if (history is null || history.ContextSnapshots.Count == 0)
        {
            return null;
        }

        var candidates = history.ContextSnapshots
            .Where(item => item.SnapshotType.Equals("scheduling_batch_result", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CapturedAtUtc);

        foreach (var snapshot in candidates)
        {
            if (string.IsNullOrWhiteSpace(snapshot.ContextJson))
            {
                continue;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<SchedulingBatchSnapshot>(snapshot.ContextJson, JsonOptions);
                if (parsed is null)
                {
                    continue;
                }

                if (parsed.ServiceRequestId != serviceRequestId)
                {
                    continue;
                }

                return parsed.Result;
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return null;
    }

    private static bool IsSchedulingStatusQuery(string? message)
    {
        var normalized = NormalizeMessage(message);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return normalized.Contains("status", StringComparison.Ordinal)
               || normalized.Contains("agendado", StringComparison.Ordinal)
               || normalized.Contains("agendada", StringComparison.Ordinal)
               || normalized.Contains("agendados", StringComparison.Ordinal)
               || normalized.Contains("agendadas", StringComparison.Ordinal);
    }

    private static bool IsProviderQuery(string? message)
    {
        var normalized = NormalizeMessage(message);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return normalized.Contains("prestador", StringComparison.Ordinal)
               || normalized.Contains("prestadores", StringComparison.Ordinal);
    }

    private static bool IsSchedulingConfirmationClaim(string? message)
    {
        var normalized = NormalizeMessage(message);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var hasNegativeSignal =
            normalized.Contains("ainda nao", StringComparison.Ordinal) ||
            normalized.Contains("nao tenho", StringComparison.Ordinal) ||
            normalized.Contains("nao consegui", StringComparison.Ordinal) ||
            normalized.Contains("nao foi", StringComparison.Ordinal);

        if (hasNegativeSignal)
        {
            return false;
        }

        return normalized.Contains("agendei", StringComparison.Ordinal) ||
               normalized.Contains("foi agendad", StringComparison.Ordinal) ||
               normalized.Contains("visitas foram agendad", StringComparison.Ordinal) ||
               normalized.Contains("confirmad", StringComparison.Ordinal);
    }

    private static string NormalizeMessage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed record QueryIntentDecision(
        string IntentName,
        Guid? ServiceRequestId,
        string? Protocol,
        int Skip,
        int Take,
        DateTime? FromUtc,
        DateTime? ToUtc);

    private sealed record QueryOrderReference(
        Guid ServiceRequestId,
        string Protocol);

    private sealed record QueryReferenceState(
        Guid? CurrentServiceRequestId,
        string? CurrentProtocol,
        IReadOnlyList<QueryOrderReference> LastListedOrders,
        string? LastQueryIntent,
        int LastOrdersSkip,
        int LastOrdersTake,
        int LastAppointmentsSkip,
        int LastAppointmentsTake,
        DateTime? LastFromUtc,
        DateTime? LastToUtc,
        DateTime UpdatedAtUtc);

    private sealed record SchedulingBatchSnapshot(
        Guid ServiceRequestId,
        TelegramChatbotBatchScheduleResultDto Result);

    private sealed record ProviderWindowAssignment(
        TelegramChatbotEligibleProviderDto Provider,
        TelegramSchedulingParseVisitWindow Window);

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
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

    private async Task PersistGuardrailInterventionAsync(
        string apiToken,
        Guid conversationId,
        TelegramChatbotGuardrailDecision decision,
        TelegramChatbotAssistantReply reply,
        CancellationToken cancellationToken)
    {
        var snapshotPayload = new
        {
            capturedAtUtc = DateTime.UtcNow,
            decision.RuleCode,
            decision.Reason,
            decision.RequiresHumanHandoff,
            reply.Intent,
            reply.NextStep
        };

        await _telegramChatbotApiClient.RegisterContextSnapshotAsync(
            apiToken,
            conversationId,
            snapshotType: "guardrail_intervention",
            contextJson: TrimToLimit(JsonSerializer.Serialize(snapshotPayload, JsonOptions), ContextPayloadLimit) ?? "{}",
            promptVersion: reply.PromptVersion,
            modelName: reply.ModelName,
            promptTokens: reply.PromptTokens,
            completionTokens: reply.CompletionTokens,
            totalTokens: reply.TotalTokens,
            intentName: reply.Intent,
            lastStep: reply.NextStep,
            cancellationToken);

        await _telegramChatbotApiClient.RegisterActionAsync(
            apiToken,
            conversationId,
            actionType: "guardrail_intervention",
            status: ActionStatusSucceeded,
            intentName: reply.Intent,
            payloadJson: TrimToLimit(JsonSerializer.Serialize(new
            {
                decision.RuleCode,
                decision.Reason
            }, JsonOptions), ContextPayloadLimit),
            resultJson: TrimToLimit(JsonSerializer.Serialize(new
            {
                reply.MessageText,
                reply.NextStep
            }, JsonOptions), ContextPayloadLimit),
            errorCode: decision.RuleCode,
            errorMessage: decision.Reason,
            correlationId: reply.CorrelationId,
            metadataJson: TrimToLimit(JsonSerializer.Serialize(new
            {
                stage = "st_010",
                origin = "telegram_bridge"
            }, JsonOptions), MetadataPayloadLimit),
            lastStep: reply.NextStep,
            conversationStatus: ConversationStatusActive,
            cancellationToken);
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
                    stage = "st_010",
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
            metadataJson: TrimToLimit(JsonSerializer.Serialize(new { stage = "st_010", origin = "telegram_bridge" }, JsonOptions), MetadataPayloadLimit),
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

    private static string FirstNonEmpty(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first.Trim();
        }

        return second?.Trim() ?? string.Empty;
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

    private sealed class NullTelegramChatbotFeatureFlagService : ITelegramChatbotFeatureFlagService
    {
        public static readonly NullTelegramChatbotFeatureFlagService Instance = new();

        public TelegramChatbotFeatureFlagDecision Evaluate(long chatId)
        {
            return new TelegramChatbotFeatureFlagDecision(
                IsEnabled: true,
                ReasonCode: "rollout_null_service",
                Bucket: null);
        }
    }

    private sealed class NullTelegramLeadAutomationClient : ITelegramLeadAutomationClient
    {
        public static readonly NullTelegramLeadAutomationClient Instance = new();

        public Task<TelegramLeadAutomationUpsertResult> UpsertClientLeadAsync(
            TelegramLeadAutomationUpsertRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TelegramLeadAutomationUpsertResult.Disabled(
                "Automacao Telegram desabilitada no ambiente atual."));
        }
    }

    private sealed class NullTelegramChatbotObservabilityService : ITelegramChatbotObservabilityService
    {
        public static readonly NullTelegramChatbotObservabilityService Instance = new();

        public void RecordInboundMessage(int attachmentCount)
        {
        }

        public void RecordOutboundMessage()
        {
        }

        public void RecordAiOutcome(TelegramChatbotAssistantReply reply, TelegramAiGatewayResult gatewayResult)
        {
        }

        public void RecordBusinessEvent(string eventName, bool success)
        {
        }

        public void RecordDependency(string dependency, bool success, long latencyMilliseconds, string? errorCode = null)
        {
        }

        public void RecordIncident(string stage, string errorCode, string? correlationId, string? message)
        {
        }

        public TelegramChatbotObservabilitySnapshotDto GetSnapshot()
        {
            return new TelegramChatbotObservabilitySnapshotDto(
                GeneratedAtUtc: DateTime.UtcNow,
                Environment: "unknown",
                Traffic: new TelegramChatbotTrafficMetricsDto(0, 0, 0),
                Ai: new TelegramChatbotAiMetricsDto(0, 0, 0, 0, 0, 0, 0, 0, 0),
                Business: new TelegramChatbotBusinessMetricsDto(0, 0, 0, 0, 0),
                Dependencies: [],
                TopErrors: [],
                RecentIncidents: []);
        }
    }
}

