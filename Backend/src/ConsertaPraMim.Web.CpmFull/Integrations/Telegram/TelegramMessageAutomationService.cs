using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Telegram;

public sealed class TelegramMessageAutomationService : ITelegramMessageAutomationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAdminKanbanService _kanbanService;
    private readonly IChatwootLeadSyncService _chatwootLeadSyncService;
    private readonly IChatwootApiClient _chatwootApiClient;
    private readonly ITelegramDeliveryQueueService _deliveryQueueService;
    private readonly ITelegramBridgeDeliveryClient _telegramBridgeDeliveryClient;
    private readonly TelegramAutomationOptions _options;
    private readonly ILogger<TelegramMessageAutomationService> _logger;

    public TelegramMessageAutomationService(
        IAdminKanbanService kanbanService,
        IChatwootLeadSyncService chatwootLeadSyncService,
        IChatwootApiClient chatwootApiClient,
        ITelegramDeliveryQueueService deliveryQueueService,
        ITelegramBridgeDeliveryClient telegramBridgeDeliveryClient,
        IOptions<TelegramAutomationOptions> options,
        ILogger<TelegramMessageAutomationService> logger)
    {
        _kanbanService = kanbanService;
        _chatwootLeadSyncService = chatwootLeadSyncService;
        _chatwootApiClient = chatwootApiClient;
        _deliveryQueueService = deliveryQueueService;
        _telegramBridgeDeliveryClient = telegramBridgeDeliveryClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TelegramInboundMessageAutomationResult> EnqueueInboundMessageAsync(
        TelegramInboundMessageAutomationRequest request,
        string providedSecret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled || !_options.MirrorMessagesEnabled)
        {
            return TelegramInboundMessageAutomationResult.Fail(
                StatusCodes.Status409Conflict,
                "Espelhamento Telegram desabilitado no ambiente atual.");
        }

        if (!IsSecretValid(providedSecret))
        {
            return TelegramInboundMessageAutomationResult.Fail(
                StatusCodes.Status401Unauthorized,
                "Chave de automacao Telegram invalida.");
        }

        if (request.TelegramChatId <= 0)
        {
            return TelegramInboundMessageAutomationResult.Fail(
                StatusCodes.Status400BadRequest,
                "TelegramChatId deve ser maior que zero para espelhamento Telegram.");
        }

        if (string.IsNullOrWhiteSpace(request.ChannelMessageId))
        {
            return TelegramInboundMessageAutomationResult.Fail(
                StatusCodes.Status400BadRequest,
                "ChannelMessageId e obrigatorio para idempotencia do espelhamento Telegram.");
        }

        if (string.IsNullOrWhiteSpace(request.MessageText) && request.Attachments.Count == 0)
        {
            return TelegramInboundMessageAutomationResult.Fail(
                StatusCodes.Status400BadRequest,
                "Mensagem Telegram sem conteudo elegivel para espelhamento.");
        }

        var leadId = ResolveLeadId(request);
        if (!leadId.HasValue)
        {
            return TelegramInboundMessageAutomationResult.Fail(
                StatusCodes.Status404NotFound,
                "Nenhum lead Telegram ativo encontrado para espelhar a mensagem.");
        }

        var lead = _kanbanService.GetLeadDetails(leadId.Value);
        if (lead is null)
        {
            return TelegramInboundMessageAutomationResult.Fail(
                StatusCodes.Status404NotFound,
                "Lead Telegram nao encontrado para espelhamento.");
        }

        if (!lead.Chatwoot.ConversationId.HasValue)
        {
            var bootstrapResult = await _chatwootLeadSyncService.SyncLeadAsync(lead.Id, cancellationToken, queueOnFailure: false);
            if (!bootstrapResult.Succeeded || !bootstrapResult.ConversationId.HasValue)
            {
                return TelegramInboundMessageAutomationResult.Fail(
                    StatusCodes.Status409Conflict,
                    $"Lead Telegram ainda nao possui conversa valida no Chatwoot. {bootstrapResult.Message}".Trim());
            }

            lead = _kanbanService.GetLeadDetails(lead.Id);
            if (lead is null || !lead.Chatwoot.ConversationId.HasValue)
            {
                return TelegramInboundMessageAutomationResult.Fail(
                    StatusCodes.Status409Conflict,
                    "Lead Telegram nao foi recarregado com conversa valida no Chatwoot apos o bootstrap.");
            }
        }

        var payload = new TelegramToChatwootDeliveryPayload
        {
            LeadId = lead.Id,
            ChatbotConversationId = request.ChatbotConversationId,
            ChannelConversationId = string.IsNullOrWhiteSpace(request.ChannelConversationId)
                ? request.TelegramChatId.ToString()
                : request.ChannelConversationId.Trim(),
            ChannelMessageId = request.ChannelMessageId.Trim(),
            TelegramChatId = request.TelegramChatId,
            SenderDisplayName = request.SenderDisplayName,
            MessageText = request.MessageText,
            SentAtUtc = request.SentAtUtc == default ? DateTime.UtcNow : request.SentAtUtc.ToUniversalTime(),
            Attachments = request.Attachments
        };

        var queueItem = _deliveryQueueService.Enqueue(
            lead.Id,
            TelegramDeliveryDirections.TelegramToChatwoot,
            BuildTelegramInboundDeliveryKey(request),
            JsonSerializer.Serialize(payload, JsonOptions),
            lead.Chatwoot.ConversationId,
            request.TelegramChatId,
            "Mensagem Telegram recebida para espelhamento no Chatwoot.",
            runImmediately: true);

        var httpStatusCode = queueItem.IsDuplicate
            ? StatusCodes.Status200OK
            : StatusCodes.Status202Accepted;

        return TelegramInboundMessageAutomationResult.Ok(
            httpStatusCode,
            new TelegramInboundMessageAutomationResponse
            {
                Success = true,
                LeadId = lead.Id,
                QueueStatus = queueItem.IsDuplicate ? "duplicate" : queueItem.Status,
                Message = queueItem.IsDuplicate
                    ? "Mensagem Telegram ja registrada anteriormente para espelhamento."
                    : "Mensagem Telegram enfileirada para espelhamento no Chatwoot.",
                Duplicate = queueItem.IsDuplicate
            });
    }

    public Task<bool> TryEnqueueOutboundMessageFromChatwootAsync(
        AdminKanbanLeadDetailsRecord lead,
        long? chatwootMessageId,
        string messageText,
        string senderName,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lead);

        if (!_options.Enabled || !_options.MirrorMessagesEnabled)
        {
            return Task.FromResult(false);
        }

        if (!IsTelegramLead(lead) || !lead.Telegram.TelegramChatId.HasValue || !lead.Chatwoot.ConversationId.HasValue)
        {
            return Task.FromResult(false);
        }

        if (string.IsNullOrWhiteSpace(messageText))
        {
            return Task.FromResult(false);
        }

        var payload = new ChatwootToTelegramDeliveryPayload
        {
            LeadId = lead.Id,
            ChatwootConversationId = lead.Chatwoot.ConversationId.Value,
            ChatwootMessageId = chatwootMessageId,
            TelegramChatId = lead.Telegram.TelegramChatId.Value,
            SenderName = senderName,
            MessageText = messageText.Trim(),
            OccurredAtUtc = occurredAtUtc == default ? DateTime.UtcNow : occurredAtUtc.ToUniversalTime(),
            ActivateHumanHandoff = _options.RequireHumanHandoffForOutbound && !lead.Telegram.HumanHandoffStartedAt.HasValue
        };

        var queueItem = _deliveryQueueService.Enqueue(
            lead.Id,
            TelegramDeliveryDirections.ChatwootToTelegram,
            BuildChatwootOutboundDeliveryKey(payload),
            JsonSerializer.Serialize(payload, JsonOptions),
            payload.ChatwootConversationId,
            payload.TelegramChatId,
            "Mensagem humana do Chatwoot recebida para entrega no Telegram.",
            runImmediately: true);

        return Task.FromResult(!queueItem.IsDuplicate);
    }

    public async Task<TelegramDeliveryProcessResult> ProcessQueueItemAsync(
        AdminKanbanTelegramDeliveryQueueItemRecord item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.Direction switch
        {
            TelegramDeliveryDirections.TelegramToChatwoot => await ProcessTelegramToChatwootAsync(item, cancellationToken),
            TelegramDeliveryDirections.ChatwootToTelegram => await ProcessChatwootToTelegramAsync(item, cancellationToken),
            _ => TelegramDeliveryProcessResult.Failed(
                $"Direcao de fila Telegram nao suportada: {item.Direction}.",
                retrySuggested: false)
        };
    }

    private async Task<TelegramDeliveryProcessResult> ProcessTelegramToChatwootAsync(
        AdminKanbanTelegramDeliveryQueueItemRecord item,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<TelegramToChatwootDeliveryPayload>(item.PayloadJson, JsonOptions);
        if (payload is null)
        {
            return TelegramDeliveryProcessResult.Failed(
                "Payload invalido na fila Telegram -> Chatwoot.",
                retrySuggested: false);
        }

        var lead = _kanbanService.GetLeadDetails(payload.LeadId);
        if (lead is null)
        {
            return TelegramDeliveryProcessResult.Failed(
                "Lead nao encontrado para entregar mensagem Telegram no Chatwoot.",
                retrySuggested: false);
        }

        if (!lead.Chatwoot.ConversationId.HasValue)
        {
            var bootstrapResult = await _chatwootLeadSyncService.SyncLeadAsync(lead.Id, cancellationToken, queueOnFailure: false);
            if (!bootstrapResult.Succeeded || !bootstrapResult.ConversationId.HasValue)
            {
                return TelegramDeliveryProcessResult.Failed(
                    bootstrapResult.Message,
                    retrySuggested: bootstrapResult.RetrySuggested);
            }

            lead = _kanbanService.GetLeadDetails(lead.Id);
            if (lead is null || !lead.Chatwoot.ConversationId.HasValue)
            {
                return TelegramDeliveryProcessResult.Failed(
                    "Lead nao foi recarregado com conversa valida no Chatwoot apos o bootstrap.",
                    retrySuggested: false);
            }
        }

        await _chatwootApiClient.CreateMessageAsync(
            lead.Chatwoot.ConversationId.Value,
            new ChatwootCreateMessageRequest
            {
                Content = BuildTelegramInboundContent(payload),
                MessageType = "incoming",
                Private = false
            },
            cancellationToken);

        _ = _kanbanService.TouchTelegramLeadLink(
            lead.Id,
            new AdminKanbanTelegramLinkTouchRequest
            {
                LastTelegramMessageSyncedAt = payload.SentAtUtc
            });
        _ = _kanbanService.AddHistoryEvent(
            lead.Id,
            "telegram_message_synced_to_chatwoot",
            $"Mensagem do Telegram espelhada para a conversa #{lead.Chatwoot.ConversationId.Value} do Chatwoot.");

        return TelegramDeliveryProcessResult.Ok("Mensagem Telegram entregue ao Chatwoot.");
    }

    private async Task<TelegramDeliveryProcessResult> ProcessChatwootToTelegramAsync(
        AdminKanbanTelegramDeliveryQueueItemRecord item,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ChatwootToTelegramDeliveryPayload>(item.PayloadJson, JsonOptions);
        if (payload is null)
        {
            return TelegramDeliveryProcessResult.Failed(
                "Payload invalido na fila Chatwoot -> Telegram.",
                retrySuggested: false);
        }

        if (payload.TelegramChatId <= 0 || string.IsNullOrWhiteSpace(payload.MessageText))
        {
            return TelegramDeliveryProcessResult.Failed(
                "Entrega Chatwoot -> Telegram sem chat destino ou sem conteudo valido.",
                retrySuggested: false);
        }

        var lead = _kanbanService.GetLeadDetails(payload.LeadId);
        if (lead is null)
        {
            return TelegramDeliveryProcessResult.Failed(
                "Lead nao encontrado para entregar mensagem humana no Telegram.",
                retrySuggested: false);
        }

        var bridgeResult = await _telegramBridgeDeliveryClient.SendHumanReplyAsync(
            new TelegramBridgeHumanReplyRequest
            {
                LeadId = payload.LeadId,
                TelegramChatId = payload.TelegramChatId,
                ChatwootConversationId = payload.ChatwootConversationId,
                ChatwootMessageId = payload.ChatwootMessageId,
                SenderName = payload.SenderName,
                MessageText = payload.MessageText,
                ActivateHumanHandoff = payload.ActivateHumanHandoff
            },
            cancellationToken);

        if (!bridgeResult.Success)
        {
            return TelegramDeliveryProcessResult.Failed(
                bridgeResult.Message,
                retrySuggested: bridgeResult.HttpStatusCode >= 500 || bridgeResult.HttpStatusCode == StatusCodes.Status429TooManyRequests);
        }

        _ = _kanbanService.TouchTelegramLeadLink(
            lead.Id,
            new AdminKanbanTelegramLinkTouchRequest
            {
                HumanHandoffStartedAt = payload.ActivateHumanHandoff ? payload.OccurredAtUtc : null,
                LastChatwootMessageSyncedAt = payload.OccurredAtUtc
            });

        if (payload.ActivateHumanHandoff && !lead.Telegram.HumanHandoffStartedAt.HasValue)
        {
            _ = _kanbanService.AddHistoryEvent(
                lead.Id,
                "chatwoot_handoff_humano_iniciado",
                $"Atendimento humano iniciado no Chatwoot para o chat Telegram #{TelegramSecuritySanitizer.MaskChatId(payload.TelegramChatId)}.");
        }

        _ = _kanbanService.AddHistoryEvent(
            lead.Id,
            "chatwoot_message_synced_to_telegram",
            $"Mensagem humana do Chatwoot entregue ao chat Telegram #{TelegramSecuritySanitizer.MaskChatId(payload.TelegramChatId)}.");

        return TelegramDeliveryProcessResult.Ok("Mensagem humana do Chatwoot entregue ao Telegram.");
    }

    private int? ResolveLeadId(TelegramInboundMessageAutomationRequest request)
    {
        if (request.ChatbotConversationId.HasValue && request.ChatbotConversationId.Value != Guid.Empty)
        {
            var leadIdByConversation = _kanbanService.FindLeadIdByTelegramChatbotConversationId(request.ChatbotConversationId.Value);
            if (leadIdByConversation.HasValue)
            {
                return leadIdByConversation;
            }
        }

        return _kanbanService.FindLeadIdByTelegramChatId(request.TelegramChatId);
    }

    private bool IsSecretValid(string providedSecret) =>
        !string.IsNullOrWhiteSpace(providedSecret) &&
        string.Equals(providedSecret.Trim(), _options.SharedSecret.Trim(), StringComparison.Ordinal);

    private static bool IsTelegramLead(AdminKanbanLeadDetailsRecord lead) =>
        string.Equals(lead.Source, "Telegram", StringComparison.OrdinalIgnoreCase) ||
        lead.Telegram.TelegramChatId.HasValue;

    private static string BuildTelegramInboundDeliveryKey(TelegramInboundMessageAutomationRequest request)
    {
        return request.ChannelMessageId.Trim();
    }

    private static string BuildChatwootOutboundDeliveryKey(ChatwootToTelegramDeliveryPayload payload)
    {
        if (payload.ChatwootMessageId.HasValue)
        {
            return $"chatwoot:{payload.ChatwootMessageId.Value}";
        }

        var raw = $"{payload.ChatwootConversationId}:{payload.OccurredAtUtc:O}:{payload.MessageText}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return $"chatwoot:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static string BuildTelegramInboundContent(TelegramToChatwootDeliveryPayload payload)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(payload.MessageText))
        {
            parts.Add(payload.MessageText.Trim());
        }

        if (payload.Attachments.Count > 0)
        {
            var attachmentSummary = payload.Attachments
                .Select(attachment => string.IsNullOrWhiteSpace(attachment.FileName)
                    ? attachment.MediaKind
                    : attachment.FileName.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            if (attachmentSummary.Count > 0)
            {
                parts.Add(attachmentSummary.Count == 1
                    ? $"[Anexo recebido no Telegram: {attachmentSummary[0]}]"
                    : $"[Anexos recebidos no Telegram: {string.Join(", ", attachmentSummary)}]");
            }
        }

        if (parts.Count == 0)
        {
            parts.Add("[Mensagem do Telegram sem texto legivel]");
        }

        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }
}
