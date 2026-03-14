using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Chatwoot;

public sealed class ChatwootWebhookService : IChatwootWebhookService
{
    private const int IncomingMessageType = 0;
    private const int OutgoingMessageType = 1;
    private const int ActivityMessageType = 2;

    private static readonly TimeSpan SignedWebhookTolerance = TimeSpan.FromMinutes(5);
    private static readonly IReadOnlySet<string> SupportedEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "message_created",
        "conversation_status_changed",
        "conversation_updated"
    };

    private readonly IAdminKanbanService _kanbanService;
    private readonly ChatwootOptions _options;
    private readonly ILogger<ChatwootWebhookService> _logger;

    public ChatwootWebhookService(
        IAdminKanbanService kanbanService,
        IOptions<ChatwootOptions> options,
        ILogger<ChatwootWebhookService> logger)
    {
        _kanbanService = kanbanService;
        _options = options.Value;
        _logger = logger;
    }

    public Task<ChatwootWebhookProcessResult> HandleAsync(ChatwootWebhookRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            return Task.FromResult(BuildAcceptedResult(
                processStatus: "ignored",
                message: "Webhook do Chatwoot ignorado porque a integracao esta desabilitada neste ambiente."));
        }

        if (request.RawBody.Length == 0)
        {
            return Task.FromResult(BuildRejectedResult(400, "Payload vazio no webhook do Chatwoot."));
        }

        if (!TryValidateSignedRequest(request, out var signatureError))
        {
            return Task.FromResult(BuildRejectedResult(401, signatureError));
        }

        var payloadJson = Encoding.UTF8.GetString(request.RawBody);
        if (!TryParsePayload(request.RawBody, out var payload, out var parseError))
        {
            return Task.FromResult(BuildRejectedResult(400, parseError));
        }

        var normalizedSignature = NormalizeSignature(request.Signature);
        var webhookEvent = _kanbanService.CreateOrGetChatwootWebhookEvent(new AdminKanbanChatwootWebhookEventUpsertRequest
        {
            ProviderEventId = BuildProviderEventId(request.DeliveryId, request.Timestamp, normalizedSignature),
            EventType = payload.EventType,
            ConversationId = payload.ConversationId,
            PayloadJson = payloadJson,
            Signature = normalizedSignature,
            ReceivedAt = DateTime.UtcNow
        });

        if (webhookEvent.IsDuplicate)
        {
            return Task.FromResult(BuildAcceptedResult(
                processStatus: "duplicate",
                message: "Evento duplicado do Chatwoot ignorado por idempotencia.",
                payload,
                webhookEventId: webhookEvent.Id,
                isDuplicate: true));
        }

        try
        {
            if (!SupportedEvents.Contains(payload.EventType))
            {
                CompleteEvent(webhookEvent.Id, "ignored", $"Evento '{payload.EventType}' nao esta mapeado nesta etapa da integracao.");
                return Task.FromResult(BuildAcceptedResult(
                    processStatus: "ignored",
                    message: $"Evento '{payload.EventType}' ignorado nesta etapa da integracao.",
                    payload,
                    webhookEventId: webhookEvent.Id));
            }

            if (!payload.ConversationId.HasValue)
            {
                CompleteEvent(webhookEvent.Id, "ignored", "Nao foi possivel identificar a conversa do webhook do Chatwoot.");
                return Task.FromResult(BuildAcceptedResult(
                    processStatus: "ignored",
                    message: "Evento do Chatwoot sem conversa identificavel.",
                    payload,
                    webhookEventId: webhookEvent.Id));
            }

            var leadId = _kanbanService.FindLeadIdByChatwootConversationId(payload.ConversationId.Value);
            if (!leadId.HasValue)
            {
                CompleteEvent(webhookEvent.Id, "ignored", $"Nenhum lead ativo encontrado para a conversa #{payload.ConversationId.Value}.");
                return Task.FromResult(BuildAcceptedResult(
                    processStatus: "ignored",
                    message: $"Nenhum lead ativo encontrado para a conversa #{payload.ConversationId.Value}.",
                    payload,
                    webhookEventId: webhookEvent.Id));
            }

            var leadUpdate = BuildLeadUpdate(payload);
            if (leadUpdate is null)
            {
                CompleteEvent(webhookEvent.Id, "ignored", "Evento do Chatwoot nao gerou atualizacao funcional no funil.");
                return Task.FromResult(BuildAcceptedResult(
                    processStatus: "ignored",
                    message: "Evento do Chatwoot recebido, mas sem impacto funcional no funil.",
                    payload,
                    leadId,
                    webhookEventId: webhookEvent.Id));
            }

            var applied = _kanbanService.ApplyChatwootWebhookLeadUpdate(leadId.Value, leadUpdate);
            if (!applied)
            {
                const string errorMessage = "Nao foi possivel aplicar a atualizacao do webhook ao lead do funil.";
                CompleteEvent(webhookEvent.Id, "failed", errorMessage);
                return Task.FromResult(new ChatwootWebhookProcessResult
                {
                    HttpStatusCode = 500,
                    Accepted = false,
                    ProcessStatus = "failed",
                    Message = errorMessage,
                    EventType = payload.EventType,
                    ConversationId = payload.ConversationId,
                    LeadId = leadId,
                    WebhookEventId = webhookEvent.Id
                });
            }

            CompleteEvent(webhookEvent.Id, "processed", null);
            return Task.FromResult(BuildAcceptedResult(
                processStatus: "processed",
                message: "Webhook do Chatwoot processado com sucesso.",
                payload,
                leadId,
                webhookEventId: webhookEvent.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar webhook do Chatwoot. Evento {EventType}, conversa {ConversationId}.", payload.EventType, payload.ConversationId);
            var sanitizedError = TrimTo(ex.Message, 500);
            CompleteEvent(webhookEvent.Id, "failed", sanitizedError);

            return Task.FromResult(new ChatwootWebhookProcessResult
            {
                HttpStatusCode = 500,
                Accepted = false,
                ProcessStatus = "failed",
                Message = sanitizedError,
                EventType = payload.EventType,
                ConversationId = payload.ConversationId,
                WebhookEventId = webhookEvent.Id
            });
        }
    }

    private bool TryValidateSignedRequest(ChatwootWebhookRequest request, out string error)
    {
        if (string.IsNullOrWhiteSpace(request.Timestamp))
        {
            error = "Header X-Chatwoot-Timestamp ausente no webhook do Chatwoot.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Signature))
        {
            error = "Header X-Chatwoot-Signature ausente no webhook do Chatwoot.";
            return false;
        }

        if (!long.TryParse(request.Timestamp.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestampUnix))
        {
            error = "Header X-Chatwoot-Timestamp invalido no webhook do Chatwoot.";
            return false;
        }

        var timestamp = DateTimeOffset.FromUnixTimeSeconds(timestampUnix);
        var drift = DateTimeOffset.UtcNow - timestamp;
        if (drift.Duration() > SignedWebhookTolerance)
        {
            error = "Webhook do Chatwoot expirado ou com timestamp fora da janela segura.";
            return false;
        }

        var expectedSignature = ComputeSignature(request.Timestamp.Trim(), request.RawBody);
        var providedSignature = NormalizeSignature(request.Signature);
        if (!FixedTimeEquals(expectedSignature, providedSignature))
        {
            error = "Assinatura invalida no webhook do Chatwoot.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private string ComputeSignature(string timestamp, byte[] rawBody)
    {
        var signedPayload = Encoding.UTF8.GetBytes($"{timestamp}.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
        var payload = new byte[signedPayload.Length + rawBody.Length];
        Buffer.BlockCopy(signedPayload, 0, payload, 0, signedPayload.Length);
        Buffer.BlockCopy(rawBody, 0, payload, signedPayload.Length, rawBody.Length);
        var hash = hmac.ComputeHash(payload);
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static bool FixedTimeEquals(string expectedSignature, string providedSignature)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature.Trim().ToLowerInvariant());
        var providedBytes = Encoding.UTF8.GetBytes(providedSignature.Trim().ToLowerInvariant());
        return expectedBytes.Length == providedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static string NormalizeSignature(string? signature)
    {
        var normalized = (signature ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return normalized.StartsWith("sha256=", StringComparison.Ordinal)
            ? normalized
            : $"sha256={normalized}";
    }

    private static bool TryParsePayload(byte[] rawBody, out ParsedChatwootWebhookPayload payload, out string error)
    {
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;

            var eventType = GetString(root, "event");
            if (string.IsNullOrWhiteSpace(eventType))
            {
                payload = default;
                error = "Payload do Chatwoot sem campo 'event'.";
                return false;
            }

            payload = new ParsedChatwootWebhookPayload
            {
                EventType = eventType.Trim(),
                ConversationId = ExtractConversationId(root, eventType),
                OccurredAt = ExtractOccurredAt(root),
                MessageType = ExtractMessageType(root),
                MessageKind = ExtractMessageKind(root),
                IsPrivate = GetBoolean(root, "private"),
                MessageContent = GetString(root, "content") ?? string.Empty,
                SenderType = ExtractSenderType(root),
                SenderName = ExtractSenderName(root),
                ConversationStatus = ExtractConversationStatus(root),
                ChangedAttributes = ExtractChangedAttributes(root)
            };
            error = string.Empty;
            return true;
        }
        catch (JsonException ex)
        {
            payload = default;
            error = $"Payload JSON invalido no webhook do Chatwoot: {ex.Message}";
            return false;
        }
    }

    private static long? ExtractConversationId(JsonElement root, string eventType)
    {
        if (TryGetInt64(root, "conversation_id", out var conversationId))
        {
            return conversationId;
        }

        if (root.TryGetProperty("conversation", out var conversation) && TryGetInt64(conversation, "id", out conversationId))
        {
            return conversationId;
        }

        if (eventType.StartsWith("conversation_", StringComparison.OrdinalIgnoreCase) && TryGetInt64(root, "id", out conversationId))
        {
            return conversationId;
        }

        return null;
    }

    private static DateTime? ExtractOccurredAt(JsonElement root)
    {
        if (TryGetDateTime(root, "created_at", out var createdAt))
        {
            return createdAt;
        }

        if (TryGetDateTime(root, "updated_at", out var updatedAt))
        {
            return updatedAt;
        }

        if (root.TryGetProperty("conversation", out var conversation) && TryGetDateTime(conversation, "last_activity_at", out var lastActivityAt))
        {
            return lastActivityAt;
        }

        return null;
    }

    private static int? ExtractMessageType(JsonElement root)
    {
        if (TryGetInt32(root, "message_type", out var messageType))
        {
            return messageType;
        }

        return null;
    }

    private static string ExtractMessageKind(JsonElement root)
    {
        if (TryGetString(root, "message_type", out var messageType))
        {
            return messageType.Trim().ToLowerInvariant() switch
            {
                "incoming" => "incoming",
                "outgoing" => "outgoing",
                "activity" => "activity",
                "template" => "template",
                _ => string.Empty
            };
        }

        return ExtractMessageType(root) switch
        {
            IncomingMessageType => "incoming",
            OutgoingMessageType => "outgoing",
            ActivityMessageType => "activity",
            _ => string.Empty
        };
    }

    private static string ExtractSenderType(JsonElement root)
    {
        if (root.TryGetProperty("sender", out var sender))
        {
            var senderType = GetString(sender, "type");
            if (!string.IsNullOrWhiteSpace(senderType))
            {
                return senderType;
            }
        }

        return GetString(root, "sender_type") ?? string.Empty;
    }

    private static string ExtractSenderName(JsonElement root)
    {
        if (root.TryGetProperty("sender", out var sender))
        {
            return GetString(sender, "name") ?? string.Empty;
        }

        return string.Empty;
    }

    private static string ExtractConversationStatus(JsonElement root)
    {
        if (TryGetString(root, "status", out var status))
        {
            return status;
        }

        if (root.TryGetProperty("conversation", out var conversation) && TryGetString(conversation, "status", out status))
        {
            return status;
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ExtractChangedAttributes(JsonElement root)
    {
        if (!root.TryGetProperty("changed_attributes", out var changedAttributes))
        {
            return [];
        }

        if (changedAttributes.ValueKind == JsonValueKind.Object)
        {
            return changedAttributes.EnumerateObject()
                .Select(item => item.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (changedAttributes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<string>();
        foreach (var item in changedAttributes.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                if (TryGetString(item, "attribute_key", out var attributeKey))
                {
                    items.Add(attributeKey);
                    continue;
                }

                foreach (var property in item.EnumerateObject())
                {
                    if (!string.Equals(property.Name, "current_value", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(property.Name, "previous_value", StringComparison.OrdinalIgnoreCase))
                    {
                        items.Add(property.Name);
                    }
                }
            }
        }

        return items
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AdminKanbanLeadWebhookUpdateRequest? BuildLeadUpdate(ParsedChatwootWebhookPayload payload)
    {
        return payload.EventType.Trim().ToLowerInvariant() switch
        {
            "message_created" => BuildMessageLeadUpdate(payload),
            "conversation_status_changed" => BuildConversationStatusLeadUpdate(payload),
            "conversation_updated" => BuildConversationUpdatedLeadUpdate(payload),
            _ => null
        };
    }

    private static AdminKanbanLeadWebhookUpdateRequest? BuildMessageLeadUpdate(ParsedChatwootWebhookPayload payload)
    {
        var messageKind = NormalizeMessageKind(payload);
        if (payload.IsPrivate || string.IsNullOrWhiteSpace(messageKind) || string.Equals(messageKind, "activity", StringComparison.Ordinal))
        {
            return null;
        }

        var eventType = string.Equals(messageKind, "incoming", StringComparison.Ordinal)
            ? "chatwoot_mensagem_recebida"
            : string.Equals(messageKind, "outgoing", StringComparison.Ordinal) || string.Equals(messageKind, "template", StringComparison.Ordinal)
                ? "chatwoot_resposta_enviada"
                : string.Empty;

        if (string.IsNullOrWhiteSpace(eventType))
        {
            return null;
        }

        var actorLabel = string.Equals(messageKind, "incoming", StringComparison.Ordinal)
            ? "Contato"
            : "Atendente";

        var description = $"{actorLabel} registrou nova mensagem na conversa do Chatwoot.";
        var preview = BuildMessagePreview(payload.MessageContent);
        if (!string.IsNullOrWhiteSpace(preview))
        {
            description = $"{description} Resumo: {preview}";
        }

        return new AdminKanbanLeadWebhookUpdateRequest
        {
            LastContactAt = payload.OccurredAt ?? DateTime.UtcNow,
            HistoryEventType = eventType,
            HistoryDescription = description
        };
    }

    private static string NormalizeMessageKind(ParsedChatwootWebhookPayload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.MessageKind))
        {
            return payload.MessageKind;
        }

        return payload.MessageType switch
        {
            IncomingMessageType => "incoming",
            OutgoingMessageType => "outgoing",
            ActivityMessageType => "activity",
            _ => string.Empty
        };
    }

    private static AdminKanbanLeadWebhookUpdateRequest BuildConversationStatusLeadUpdate(ParsedChatwootWebhookPayload payload) =>
        new()
        {
            HistoryEventType = "chatwoot_status_alterado",
            HistoryDescription = $"Conversa do Chatwoot mudou para status '{FormatConversationStatusLabel(payload.ConversationStatus)}'."
        };

    private static AdminKanbanLeadWebhookUpdateRequest BuildConversationUpdatedLeadUpdate(ParsedChatwootWebhookPayload payload)
    {
        var summary = BuildConversationUpdatedSummary(payload.ChangedAttributes);
        return new AdminKanbanLeadWebhookUpdateRequest
        {
            HistoryEventType = "chatwoot_conversa_atualizada",
            HistoryDescription = $"Conversa do Chatwoot recebeu atualizacao operacional. {summary}".Trim()
        };
    }

    private static string BuildConversationUpdatedSummary(IReadOnlyList<string> changedAttributes)
    {
        if (changedAttributes.Count == 0)
        {
            return string.Empty;
        }

        var mapped = changedAttributes
            .Select(MapChangedAttributeLabel)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (mapped.Count == 0)
        {
            return string.Empty;
        }

        return $"Campos atualizados: {string.Join(", ", mapped)}.";
    }

    private static string MapChangedAttributeLabel(string attributeName) =>
        attributeName.Trim().ToLowerInvariant() switch
        {
            "status" => "status do atendimento",
            "assignee_id" => "responsavel do atendimento",
            "labels" => "etiquetas",
            "custom_attributes" => "atributos operacionais",
            "contact_last_seen_at" => "ultima atividade do contato",
            _ => string.Empty
        };

    private static string BuildMessagePreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = content.Trim().ReplaceLineEndings(" ");
        return TrimTo(normalized, 160);
    }

    private bool CompleteEvent(int webhookEventId, string processStatus, string? errorMessage)
    {
        return _kanbanService.CompleteChatwootWebhookEvent(webhookEventId, processStatus, errorMessage);
    }

    private static string BuildProviderEventId(string? deliveryId, string timestamp, string normalizedSignature)
    {
        if (!string.IsNullOrWhiteSpace(deliveryId))
        {
            return deliveryId.Trim();
        }

        var signatureFragment = normalizedSignature.Replace("sha256=", string.Empty, StringComparison.Ordinal);
        return TrimTo($"sig:{timestamp.Trim()}:{signatureFragment}", 120);
    }

    private static ChatwootWebhookProcessResult BuildAcceptedResult(
        string processStatus,
        string message,
        ParsedChatwootWebhookPayload payload = default,
        int? leadId = null,
        int? webhookEventId = null,
        bool isDuplicate = false) =>
        new()
        {
            HttpStatusCode = 200,
            Accepted = true,
            ProcessStatus = processStatus,
            Message = message,
            EventType = payload.EventType ?? string.Empty,
            ConversationId = payload.ConversationId,
            LeadId = leadId,
            WebhookEventId = webhookEventId,
            IsDuplicate = isDuplicate
        };

    private static ChatwootWebhookProcessResult BuildRejectedResult(int httpStatusCode, string message) =>
        new()
        {
            HttpStatusCode = httpStatusCode,
            Accepted = false,
            ProcessStatus = "rejected",
            Message = message
        };

    private static string FormatConversationStatusLabel(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "open" => "aberta",
            "pending" => "pendente",
            "resolved" => "resolvida",
            "snoozed" => "adiada",
            _ => "atualizada"
        };

    private static string TrimTo(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = GetString(element, propertyName) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };
    }

    private static bool GetBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var parsed) => parsed,
            _ => false
        };
    }

    private static bool TryGetInt64(JsonElement element, string propertyName, out long value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.TryGetInt64(out value),
            JsonValueKind.String => long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value),
            _ => false
        };
    }

    private static bool TryGetInt32(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.TryGetInt32(out value),
            JsonValueKind.String => int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value),
            _ => false
        };
    }

    private static bool TryGetDateTime(JsonElement element, string propertyName, out DateTime value)
    {
        value = default;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var unixValue))
        {
            value = DateTimeOffset.FromUnixTimeSeconds(unixValue).UtcDateTime;
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = property.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedOffset))
        {
            value = parsedOffset.UtcDateTime;
            return true;
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedDateTime))
        {
            value = DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Utc);
            return true;
        }

        return false;
    }

    private readonly record struct ParsedChatwootWebhookPayload
    {
        public string EventType { get; init; }
        public long? ConversationId { get; init; }
        public DateTime? OccurredAt { get; init; }
        public int? MessageType { get; init; }
        public string MessageKind { get; init; }
        public bool IsPrivate { get; init; }
        public string MessageContent { get; init; }
        public string SenderType { get; init; }
        public string SenderName { get; init; }
        public string ConversationStatus { get; init; }
        public IReadOnlyList<string> ChangedAttributes { get; init; }
    }
}
