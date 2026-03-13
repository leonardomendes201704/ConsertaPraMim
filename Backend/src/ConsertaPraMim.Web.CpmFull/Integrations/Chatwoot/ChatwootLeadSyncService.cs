using System.Globalization;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Chatwoot;

public sealed class ChatwootLeadSyncService : IChatwootLeadSyncService
{
    private readonly IAdminKanbanService _kanbanService;
    private readonly IChatwootApiClient _chatwootApiClient;
    private readonly ChatwootOptions _options;
    private readonly ILogger<ChatwootLeadSyncService> _logger;

    public ChatwootLeadSyncService(
        IAdminKanbanService kanbanService,
        IChatwootApiClient chatwootApiClient,
        IOptions<ChatwootOptions> options,
        ILogger<ChatwootLeadSyncService> logger)
    {
        _kanbanService = kanbanService;
        _chatwootApiClient = chatwootApiClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ChatwootLeadSyncResult> SyncLeadAsync(int leadId, CancellationToken cancellationToken = default)
    {
        var lead = _kanbanService.GetLeadDetails(leadId);
        if (lead is null)
        {
            return ChatwootLeadSyncResult.NotFound("Lead nao encontrado para sincronizacao com Chatwoot.");
        }

        var inboxId = ResolveInboxId(lead.BoardType);
        var lastSyncAt = DateTime.UtcNow;

        if (!_options.Enabled)
        {
            _kanbanService.UpdateLeadChatwootSync(
                leadId,
                new AdminKanbanLeadChatwootSyncUpdateRequest
                {
                    ChatwootInboxId = inboxId,
                    ChatwootSyncStatus = ChatwootSyncStatuses.Disabled,
                    ChatwootLastSyncAt = lastSyncAt,
                    ClearChatwootLastError = true
                });

            return ChatwootLeadSyncResult.Disabled(
                "Integracao com Chatwoot desabilitada no ambiente atual.",
                lead.Chatwoot.ContactId,
                lead.Chatwoot.ConversationId,
                inboxId);
        }

        if (!TryBuildContactRequest(lead, inboxId, out var contactRequest, out var validationError))
        {
            var sanitizedError = TrimTo(validationError, 500);
            _kanbanService.UpdateLeadChatwootSync(
                leadId,
                new AdminKanbanLeadChatwootSyncUpdateRequest
                {
                    ChatwootInboxId = inboxId,
                    ChatwootSyncStatus = ChatwootSyncStatuses.Failed,
                    ChatwootLastSyncAt = lastSyncAt,
                    ChatwootLastError = sanitizedError
                });
            _kanbanService.AddHistoryEvent(leadId, "chatwoot_sync_falhou", sanitizedError);

            return ChatwootLeadSyncResult.Failed(
                sanitizedError,
                lead.Chatwoot.ContactId,
                lead.Chatwoot.ConversationId,
                inboxId);
        }

        long? contactId = lead.Chatwoot.ContactId;
        long? conversationId = lead.Chatwoot.ConversationId;

        try
        {
            var resolvedContact = await ResolveContactAsync(lead, contactRequest!, inboxId, cancellationToken);
            contactId = resolvedContact.Contact.Id;

            if (!conversationId.HasValue)
            {
                var conversation = await _chatwootApiClient.CreateConversationAsync(
                    new ChatwootCreateConversationRequest
                    {
                        SourceId = resolvedContact.ContactInbox.SourceId,
                        InboxId = inboxId,
                        ContactId = resolvedContact.Contact.Id,
                        Status = "open"
                    },
                    cancellationToken);

                conversationId = conversation.Id;

                await _chatwootApiClient.CreateMessageAsync(
                    conversation.Id,
                    new ChatwootCreateMessageRequest
                    {
                        Content = BuildOpeningMessage(lead),
                        MessageType = "outgoing",
                        Private = true
                    },
                    cancellationToken);
            }

            _kanbanService.UpdateLeadChatwootSync(
                leadId,
                new AdminKanbanLeadChatwootSyncUpdateRequest
                {
                    ChatwootContactId = contactId,
                    ChatwootConversationId = conversationId,
                    ChatwootInboxId = inboxId,
                    ChatwootSyncStatus = ChatwootSyncStatuses.Synced,
                    ChatwootLastSyncAt = lastSyncAt,
                    ClearChatwootLastError = true
                });

            if (!lead.Chatwoot.ContactId.HasValue || lead.Chatwoot.ContactId.Value != contactId.Value)
            {
                _kanbanService.AddHistoryEvent(
                    leadId,
                    "chatwoot_contato_sincronizado",
                    $"Contato #{contactId.Value} sincronizado com o Chatwoot.");
            }

            if (!lead.Chatwoot.ConversationId.HasValue && conversationId.HasValue)
            {
                _kanbanService.AddHistoryEvent(
                    leadId,
                    "chatwoot_conversa_criada",
                    $"Conversa #{conversationId.Value} criada no inbox #{inboxId} do Chatwoot.");
            }

            if (lead.Chatwoot.SyncStatus != ChatwootSyncStatuses.Synced)
            {
                _kanbanService.AddHistoryEvent(
                    leadId,
                    "chatwoot_sincronizado",
                    "Lead sincronizado com Chatwoot e pronto para atendimento.");
            }

            return ChatwootLeadSyncResult.Synced(
                "Lead sincronizado com Chatwoot.",
                contactId,
                conversationId,
                inboxId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao sincronizar lead {LeadId} com Chatwoot.", leadId);

            var sanitizedError = TrimTo(BuildUserFacingError(ex), 500);
            _kanbanService.UpdateLeadChatwootSync(
                leadId,
                new AdminKanbanLeadChatwootSyncUpdateRequest
                {
                    ChatwootContactId = contactId,
                    ChatwootConversationId = conversationId,
                    ChatwootInboxId = inboxId,
                    ChatwootSyncStatus = ChatwootSyncStatuses.Failed,
                    ChatwootLastSyncAt = lastSyncAt,
                    ChatwootLastError = sanitizedError
                });
            _kanbanService.AddHistoryEvent(
                leadId,
                "chatwoot_sync_falhou",
                $"Falha na sincronizacao com Chatwoot: {sanitizedError}");

            return ChatwootLeadSyncResult.Failed(sanitizedError, contactId, conversationId, inboxId);
        }
    }

    private async Task<ResolvedChatwootContact> ResolveContactAsync(
        AdminKanbanLeadDetailsRecord lead,
        ChatwootUpsertContactRequest contactRequest,
        long inboxId,
        CancellationToken cancellationToken)
    {
        ChatwootContactSummary? contact = null;

        if (lead.Chatwoot.ContactId.HasValue)
        {
            contact = await _chatwootApiClient.GetContactAsync(lead.Chatwoot.ContactId.Value, cancellationToken);
        }

        if (contact is null)
        {
            foreach (var query in BuildSearchQueries(lead, contactRequest))
            {
                var found = await _chatwootApiClient.SearchContactsAsync(query, cancellationToken);
                contact = found.FirstOrDefault();
                if (contact is not null)
                {
                    break;
                }
            }
        }

        contact = contact is null
            ? await _chatwootApiClient.CreateContactAsync(contactRequest, cancellationToken)
            : await _chatwootApiClient.UpdateContactAsync(contact.Id, contactRequest, cancellationToken);

        var contactInbox = contact.ContactInboxes.FirstOrDefault(item => item.InboxId == inboxId);
        if (contactInbox is null)
        {
            contactInbox = await _chatwootApiClient.CreateContactInboxAsync(
                contact.Id,
                new ChatwootCreateContactInboxRequest
                {
                    InboxId = inboxId,
                    SourceId = BuildSourceId(lead)
                },
                cancellationToken);
        }

        return new ResolvedChatwootContact(contact, contactInbox);
    }

    private static bool TryBuildContactRequest(
        AdminKanbanLeadDetailsRecord lead,
        long inboxId,
        out ChatwootUpsertContactRequest? request,
        out string error)
    {
        var normalizedPhone = NormalizePhoneNumber(lead.Phone);
        var normalizedEmail = NormalizeEmail(lead.Email);

        if (string.IsNullOrWhiteSpace(normalizedPhone) && string.IsNullOrWhiteSpace(normalizedEmail))
        {
            request = null;
            error = "Lead sem telefone ou e-mail valido para sincronizar com Chatwoot.";
            return false;
        }

        request = new ChatwootUpsertContactRequest
        {
            InboxId = inboxId,
            Name = string.IsNullOrWhiteSpace(lead.Name) ? $"Lead #{lead.Id}" : TrimTo(lead.Name, 140),
            Email = normalizedEmail,
            PhoneNumber = normalizedPhone,
            Identifier = BuildContactIdentifier(lead, normalizedPhone, normalizedEmail),
            AdditionalAttributes = BuildAdditionalAttributes(lead),
            CustomAttributes = BuildCustomAttributes(lead)
        };
        error = string.Empty;
        return true;
    }

    private static Dictionary<string, object?> BuildAdditionalAttributes(AdminKanbanLeadDetailsRecord lead)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["board_type"] = lead.BoardType,
            ["service_category"] = NullIfWhiteSpace(lead.ServiceCategory),
            ["source"] = NullIfWhiteSpace(lead.Source),
            ["city"] = NullIfWhiteSpace(lead.City),
            ["postal_code"] = NullIfWhiteSpace(lead.PostalCode),
            ["status_note"] = NullIfWhiteSpace(TrimTo(lead.StatusNote, 300)),
            ["last_contact_at_utc"] = lead.LastContactAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
        };

        return attributes
            .Where(item => item.Value is not null)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object?> BuildCustomAttributes(AdminKanbanLeadDetailsRecord lead)
    {
        var attributes = new Dictionary<string, object?>
        {
            ["cpm_lead_id"] = lead.Id,
            ["cpm_board_type"] = lead.BoardType,
            ["cpm_stage_name"] = NullIfWhiteSpace(lead.StageName)
        };

        return attributes;
    }

    private static IReadOnlyList<string> BuildSearchQueries(AdminKanbanLeadDetailsRecord lead, ChatwootUpsertContactRequest request)
    {
        var queries = new List<string>();

        if (!string.IsNullOrWhiteSpace(lead.Chatwoot.ContactId?.ToString(CultureInfo.InvariantCulture)))
        {
            queries.Add(lead.Chatwoot.ContactId.Value.ToString(CultureInfo.InvariantCulture));
        }

        queries.Add(request.Identifier);

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            queries.Add(request.Email);
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            queries.Add(request.PhoneNumber);
            queries.Add(request.PhoneNumber.TrimStart('+'));
        }

        return queries
            .Where(query => !string.IsNullOrWhiteSpace(query))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private long ResolveInboxId(string boardType) =>
        AdminKanbanBoardTypes.Normalize(boardType) switch
        {
            AdminKanbanBoardTypes.Clients => _options.ClientsInboxId,
            AdminKanbanBoardTypes.Providers => _options.ProvidersInboxId,
            _ => throw new InvalidOperationException("Tipo de funil sem inbox Chatwoot configurado.")
        };

    private static string BuildContactIdentifier(AdminKanbanLeadDetailsRecord lead, string? normalizedPhone, string? normalizedEmail)
    {
        if (!string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return $"phone:{normalizedPhone}";
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return $"email:{normalizedEmail}";
        }

        return $"cpm-lead:{lead.Id}";
    }

    private static string BuildSourceId(AdminKanbanLeadDetailsRecord lead) =>
        $"cpm-lead-{lead.BoardType}-{lead.Id}";

    private static string BuildOpeningMessage(AdminKanbanLeadDetailsRecord lead)
    {
        var lines = new List<string>
        {
            "Novo lead recebido no funil do ConsertaPraMim.",
            $"Lead ID: {lead.Id}",
            $"Funil: {AdminKanbanBoardTypes.GetTitle(lead.BoardType)}",
            $"Etapa atual: {lead.StageName}",
            $"Nome: {lead.Name}"
        };

        if (!string.IsNullOrWhiteSpace(lead.Phone))
        {
            lines.Add($"Telefone: {lead.Phone}");
        }

        if (!string.IsNullOrWhiteSpace(lead.Email))
        {
            lines.Add($"E-mail: {lead.Email}");
        }

        if (!string.IsNullOrWhiteSpace(lead.ServiceCategory))
        {
            lines.Add($"Servico: {lead.ServiceCategory}");
        }

        var location = string.Join(" / ", new[] { lead.PostalCode, lead.City }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (!string.IsNullOrWhiteSpace(location))
        {
            lines.Add($"CEP/Cidade: {location}");
        }

        if (!string.IsNullOrWhiteSpace(lead.Source))
        {
            lines.Add($"Fonte: {lead.Source}");
        }

        if (!string.IsNullOrWhiteSpace(lead.StatusNote))
        {
            lines.Add($"Observacao inicial: {TrimTo(lead.StatusNote, 300)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildUserFacingError(Exception ex) =>
        ex switch
        {
            ChatwootApiException apiEx => apiEx.Message,
            HttpRequestException => "Falha de rede ao acessar o Chatwoot.",
            TaskCanceledException => "Tempo esgotado ao acessar o Chatwoot.",
            _ => ex.Message
        };

    private static string? NormalizePhoneNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return null;
        }

        if (digits.StartsWith("55", StringComparison.Ordinal) && digits.Length is 12 or 13)
        {
            return $"+{digits}";
        }

        if (digits.Length is 10 or 11)
        {
            return $"+55{digits}";
        }

        if (digits.Length is >= 12 and <= 15)
        {
            return $"+{digits}";
        }

        return null;
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = email.Trim().ToLowerInvariant();
        return normalized.Contains('@', StringComparison.Ordinal) ? normalized : null;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string TrimTo(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private sealed record ResolvedChatwootContact(
        ChatwootContactSummary Contact,
        ChatwootContactInboxSummary ContactInbox);
}
