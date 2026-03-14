using System.Globalization;
using AppMobileCPM.Observability;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Chatwoot;

public sealed class ChatwootLeadSyncService : IChatwootLeadSyncService
{
    private const string ManagedLabelPrefix = "cpm_";

    private readonly IAdminKanbanService _kanbanService;
    private readonly IChatwootApiClient _chatwootApiClient;
    private readonly IChatwootSyncQueueService _chatwootSyncQueueService;
    private readonly ChatwootOptions _options;
    private readonly ILogger<ChatwootLeadSyncService> _logger;

    public ChatwootLeadSyncService(
        IAdminKanbanService kanbanService,
        IChatwootApiClient chatwootApiClient,
        IChatwootSyncQueueService chatwootSyncQueueService,
        IOptions<ChatwootOptions> options,
        ILogger<ChatwootLeadSyncService> logger)
    {
        _kanbanService = kanbanService;
        _chatwootApiClient = chatwootApiClient;
        _chatwootSyncQueueService = chatwootSyncQueueService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ChatwootLeadSyncResult> SyncLeadAsync(int leadId, CancellationToken cancellationToken = default, bool queueOnFailure = true)
    {
        using var correlationScope = ChatwootCorrelationContext.Push(ChatwootCorrelationContext.Current ?? ChatwootCorrelationContext.Create($"lead-sync-{leadId}"));
        var correlationId = ChatwootCorrelationContext.Current ?? ChatwootCorrelationContext.GetOrCreate($"lead-sync-{leadId}");
        using var loggerScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["LeadId"] = leadId,
            ["Flow"] = "ChatwootLeadSync"
        });

        var lead = _kanbanService.GetLeadDetails(leadId);
        if (lead is null)
        {
            _logger.LogWarning("Sync do Chatwoot ignorada porque o lead nao foi encontrado. CorrelationId={CorrelationId}", correlationId);
            return ChatwootLeadSyncResult.NotFound("Lead nao encontrado para sincronizacao com Chatwoot.");
        }

        var inboxId = ResolveInboxId(lead.BoardType);
        var lastSyncAt = DateTime.UtcNow;
        _logger.LogInformation(
            "Iniciando sincronizacao do lead com Chatwoot. CorrelationId={CorrelationId} LeadId={LeadId} BoardType={BoardType} InboxId={InboxId} QueueOnFailure={QueueOnFailure}",
            correlationId,
            leadId,
            lead.BoardType,
            inboxId,
            queueOnFailure);

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

            _logger.LogInformation(
                "Integracao Chatwoot desabilitada; lead marcado como disabled. CorrelationId={CorrelationId} LeadId={LeadId} InboxId={InboxId}",
                correlationId,
                leadId,
                inboxId);
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
            _logger.LogWarning(
                "Lead bloqueado na validacao antes da chamada ao Chatwoot. CorrelationId={CorrelationId} LeadId={LeadId} ValidationError={ValidationError}",
                correlationId,
                leadId,
                sanitizedError);

            return ChatwootLeadSyncResult.Failed(
                sanitizedError,
                lead.Chatwoot.ContactId,
                lead.Chatwoot.ConversationId,
                inboxId,
                retrySuggested: false);
        }

        long? contactId = lead.Chatwoot.ContactId;
        long? conversationId = lead.Chatwoot.ConversationId;
        var createdConversation = false;

        try
        {
            var resolvedContact = await ResolveContactAsync(lead, contactRequest!, inboxId, cancellationToken);
            contactId = resolvedContact.Contact.Id;

            if (!conversationId.HasValue)
            {
                var resolvedConversation = await ResolveConversationAsync(
                    lead,
                    resolvedContact,
                    inboxId,
                    cancellationToken);

                conversationId = resolvedConversation.ConversationId;
                createdConversation = resolvedConversation.CreatedNewConversation;
            }

            await ApplyStageMappingAsync(lead, conversationId.Value, trackHistory: false, cancellationToken);
            await UpdateContactProjectionAsync(lead, contactId.Value, cancellationToken);

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
                    createdConversation ? "chatwoot_conversa_criada" : "chatwoot_conversa_reaproveitada",
                    createdConversation
                        ? $"Conversa #{conversationId.Value} criada no inbox #{inboxId} do Chatwoot."
                        : $"Conversa #{conversationId.Value} reaproveitada no inbox #{inboxId} do Chatwoot.");
            }

            if (lead.Chatwoot.SyncStatus != ChatwootSyncStatuses.Synced)
            {
                _kanbanService.AddHistoryEvent(
                    leadId,
                    "chatwoot_sincronizado",
                    "Lead sincronizado com Chatwoot e pronto para atendimento.");
            }

            TryCompleteActiveRetries(
                leadId,
                [ChatwootSyncOperationTypes.LeadSync, ChatwootSyncOperationTypes.StageSync]);
            _logger.LogInformation(
                "Lead sincronizado com Chatwoot. CorrelationId={CorrelationId} LeadId={LeadId} ContactId={ContactId} ConversationId={ConversationId} InboxId={InboxId} CreatedConversation={CreatedConversation}",
                correlationId,
                leadId,
                contactId,
                conversationId,
                inboxId,
                createdConversation);

            return ChatwootLeadSyncResult.Synced(
                "Lead sincronizado com Chatwoot.",
                contactId,
                conversationId,
                inboxId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao sincronizar lead com Chatwoot. CorrelationId={CorrelationId} LeadId={LeadId} InboxId={InboxId}",
                correlationId,
                leadId,
                inboxId);

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

            var queuedForRetry = false;
            var message = sanitizedError;
            if (queueOnFailure && _options.Enabled)
            {
                queuedForRetry = TryEnqueueRetry(leadId, ChatwootSyncOperationTypes.LeadSync, sanitizedError);
                if (queuedForRetry)
                {
                    message = $"{sanitizedError} Retentativa automatica enfileirada.";
                }
            }

            return ChatwootLeadSyncResult.Failed(
                message,
                contactId,
                conversationId,
                inboxId,
                retrySuggested: true,
                queuedForRetry: queuedForRetry);
        }
    }

    public async Task<ChatwootLeadSyncResult> SyncLeadStageAsync(int leadId, CancellationToken cancellationToken = default, bool queueOnFailure = true)
    {
        using var correlationScope = ChatwootCorrelationContext.Push(ChatwootCorrelationContext.Current ?? ChatwootCorrelationContext.Create($"stage-sync-{leadId}"));
        var correlationId = ChatwootCorrelationContext.Current ?? ChatwootCorrelationContext.GetOrCreate($"stage-sync-{leadId}");
        using var loggerScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["LeadId"] = leadId,
            ["Flow"] = "ChatwootStageSync"
        });

        var lead = _kanbanService.GetLeadDetails(leadId);
        if (lead is null)
        {
            _logger.LogWarning("Sync de etapa ignorada porque o lead nao foi encontrado. CorrelationId={CorrelationId}", correlationId);
            return ChatwootLeadSyncResult.NotFound("Lead nao encontrado para sincronizar etapa com o Chatwoot.");
        }

        _logger.LogInformation(
            "Iniciando sincronizacao de etapa no Chatwoot. CorrelationId={CorrelationId} LeadId={LeadId} StageName={StageName} QueueOnFailure={QueueOnFailure}",
            correlationId,
            leadId,
            lead.StageName,
            queueOnFailure);

        if (!lead.Chatwoot.ConversationId.HasValue)
        {
            var bootstrapResult = await SyncLeadAsync(leadId, cancellationToken, queueOnFailure: false);
            if (!bootstrapResult.Succeeded || !bootstrapResult.ConversationId.HasValue)
            {
                if (queueOnFailure && bootstrapResult.RetrySuggested && _options.Enabled)
                {
                    var queuedForRetry = TryEnqueueRetry(leadId, ChatwootSyncOperationTypes.StageSync, bootstrapResult.Message);

                    return ChatwootLeadSyncResult.Failed(
                        queuedForRetry
                            ? $"{bootstrapResult.Message} Retentativa automatica enfileirada para sincronizacao da etapa."
                            : bootstrapResult.Message,
                        bootstrapResult.ContactId,
                        bootstrapResult.ConversationId,
                        bootstrapResult.InboxId,
                        retrySuggested: true,
                        queuedForRetry: queuedForRetry);
                }

                return bootstrapResult;
            }

            lead = _kanbanService.GetLeadDetails(leadId);
            if (lead is null || !lead.Chatwoot.ConversationId.HasValue)
            {
                return ChatwootLeadSyncResult.Failed(
                    "Nao foi possivel recarregar o lead apos sincronizar a conversa no Chatwoot.",
                    bootstrapResult.ContactId,
                    bootstrapResult.ConversationId,
                    bootstrapResult.InboxId);
            }
        }

        try
        {
            await ApplyStageMappingAsync(lead, lead.Chatwoot.ConversationId.Value, trackHistory: true, cancellationToken);
            await UpdateContactProjectionAsync(lead, lead.Chatwoot.ContactId, cancellationToken);
            await AppendStageSyncHistoryMessageAsync(lead, lead.Chatwoot.ConversationId.Value, cancellationToken);

            _kanbanService.UpdateLeadChatwootSync(
                leadId,
                new AdminKanbanLeadChatwootSyncUpdateRequest
                {
                    ChatwootContactId = lead.Chatwoot.ContactId,
                    ChatwootConversationId = lead.Chatwoot.ConversationId,
                    ChatwootInboxId = lead.Chatwoot.InboxId ?? ResolveInboxId(lead.BoardType),
                    ChatwootSyncStatus = ChatwootSyncStatuses.Synced,
                    ChatwootLastSyncAt = DateTime.UtcNow,
                    ClearChatwootLastError = true
                });

            TryCompleteActiveRetries(
                leadId,
                [ChatwootSyncOperationTypes.StageSync, ChatwootSyncOperationTypes.LeadSync]);
            _logger.LogInformation(
                "Etapa sincronizada com Chatwoot. CorrelationId={CorrelationId} LeadId={LeadId} ConversationId={ConversationId} StageName={StageName}",
                correlationId,
                leadId,
                lead.Chatwoot.ConversationId,
                lead.StageName);

            return ChatwootLeadSyncResult.Synced(
                $"Etapa '{lead.StageName}' sincronizada com Chatwoot.",
                lead.Chatwoot.ContactId,
                lead.Chatwoot.ConversationId,
                lead.Chatwoot.InboxId ?? ResolveInboxId(lead.BoardType));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao sincronizar etapa do lead com Chatwoot. CorrelationId={CorrelationId} LeadId={LeadId} StageName={StageName} ConversationId={ConversationId}",
                correlationId,
                leadId,
                lead.StageName,
                lead.Chatwoot.ConversationId);

            var sanitizedError = TrimTo(BuildUserFacingError(ex), 500);
            _kanbanService.UpdateLeadChatwootSync(
                leadId,
                new AdminKanbanLeadChatwootSyncUpdateRequest
                {
                    ChatwootContactId = lead.Chatwoot.ContactId,
                    ChatwootConversationId = lead.Chatwoot.ConversationId,
                    ChatwootInboxId = lead.Chatwoot.InboxId ?? ResolveInboxId(lead.BoardType),
                    ChatwootSyncStatus = ChatwootSyncStatuses.Failed,
                    ChatwootLastSyncAt = DateTime.UtcNow,
                    ChatwootLastError = sanitizedError
                });
            _kanbanService.AddHistoryEvent(
                leadId,
                "chatwoot_etapa_sync_falhou",
                $"Falha ao sincronizar etapa '{lead.StageName}' com Chatwoot: {sanitizedError}");

            var queuedForRetry = false;
            var message = sanitizedError;
            if (queueOnFailure && _options.Enabled)
            {
                queuedForRetry = TryEnqueueRetry(leadId, ChatwootSyncOperationTypes.StageSync, sanitizedError);
                if (queuedForRetry)
                {
                    message = $"{sanitizedError} Retentativa automatica enfileirada para sincronizacao da etapa.";
                }
            }

            return ChatwootLeadSyncResult.Failed(
                message,
                lead.Chatwoot.ContactId,
                lead.Chatwoot.ConversationId,
                lead.Chatwoot.InboxId ?? ResolveInboxId(lead.BoardType),
                retrySuggested: true,
                queuedForRetry: queuedForRetry);
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
        var sourceMapping = ChatwootLeadSourceMappings.Resolve(lead.Source);
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["board_type"] = lead.BoardType,
            ["service_category"] = NullIfWhiteSpace(lead.ServiceCategory),
            ["source"] = NullIfWhiteSpace(lead.Source),
            ["source_display"] = sourceMapping?.DisplayName,
            ["source_slug"] = sourceMapping?.Slug,
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
        var stageMapping = ChatwootStageMappings.Resolve(lead.BoardType, lead.StageName);
        var sourceMapping = ChatwootLeadSourceMappings.Resolve(lead.Source);
        var attributes = new Dictionary<string, object?>
        {
            ["cpm_lead_id"] = lead.Id,
            ["cpm_board_type"] = lead.BoardType,
            ["cpm_stage_name"] = NullIfWhiteSpace(lead.StageName),
            ["cpm_stage_slug"] = stageMapping.StageSlug,
            ["cpm_lead_source"] = sourceMapping?.DisplayName,
            ["cpm_lead_source_slug"] = sourceMapping?.Slug
        };

        return attributes
            .Where(item => item.Value is not null)
            .ToDictionary(item => item.Key, item => item.Value);
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
        var sourceMapping = ChatwootLeadSourceMappings.Resolve(lead.Source);
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

        if (sourceMapping is not null)
        {
            lines.Add($"Canal de origem: {sourceMapping.DisplayName}");
            if (!string.Equals(sourceMapping.RawValue, sourceMapping.DisplayName, StringComparison.Ordinal))
            {
                lines.Add($"Fonte original informada: {sourceMapping.RawValue}");
            }
        }

        if (!string.IsNullOrWhiteSpace(lead.StatusNote))
        {
            lines.Add($"Observacao inicial: {TrimTo(lead.StatusNote, 300)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private async Task<ResolvedChatwootConversation> ResolveConversationAsync(
        AdminKanbanLeadDetailsRecord lead,
        ResolvedChatwootContact resolvedContact,
        long inboxId,
        CancellationToken cancellationToken)
    {
        var existingConversation = await FindExistingConversationAsync(resolvedContact.Contact.Id, inboxId, cancellationToken);
        if (existingConversation is not null)
        {
            return new ResolvedChatwootConversation(existingConversation.Id, false);
        }

        var createdConversation = await _chatwootApiClient.CreateConversationAsync(
            new ChatwootCreateConversationRequest
            {
                SourceId = resolvedContact.ContactInbox.SourceId,
                InboxId = inboxId,
                ContactId = resolvedContact.Contact.Id,
                Status = "open"
            },
            cancellationToken);

        await _chatwootApiClient.CreateMessageAsync(
            createdConversation.Id,
            new ChatwootCreateMessageRequest
            {
                Content = BuildOpeningMessage(lead),
                MessageType = "outgoing",
                Private = true
            },
            cancellationToken);

        return new ResolvedChatwootConversation(createdConversation.Id, true);
    }

    private async Task<ChatwootConversationSummary?> FindExistingConversationAsync(
        long contactId,
        long inboxId,
        CancellationToken cancellationToken)
    {
        var conversations = await _chatwootApiClient.ListContactConversationsAsync(contactId, cancellationToken);

        return conversations
            .Where(item => item.InboxId == inboxId)
            .OrderByDescending(item => item.Id)
            .FirstOrDefault();
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

    private async Task ApplyStageMappingAsync(
        AdminKanbanLeadDetailsRecord lead,
        long conversationId,
        bool trackHistory,
        CancellationToken cancellationToken)
    {
        var mapping = ChatwootStageMappings.Resolve(lead.BoardType, lead.StageName);

        await _chatwootApiClient.UpdateConversationCustomAttributesAsync(
            conversationId,
            BuildCustomAttributes(lead),
            cancellationToken);

        await SyncManagedLabelsAsync(
            listLabels: token => _chatwootApiClient.ListConversationLabelsAsync(conversationId, token),
            replaceLabels: (labels, token) => _chatwootApiClient.ReplaceConversationLabelsAsync(conversationId, labels, token),
            mapping,
            cancellationToken);
        await _chatwootApiClient.UpdateConversationStatusAsync(conversationId, mapping.ConversationStatus, cancellationToken);

        if (trackHistory)
        {
            _kanbanService.AddHistoryEvent(
                lead.Id,
                "chatwoot_etapa_sincronizada",
                $"Etapa '{lead.StageName}' sincronizada no Chatwoot com status '{FormatConversationStatusLabel(mapping.ConversationStatus)}'.");
        }
    }

    private async Task UpdateContactProjectionAsync(
        AdminKanbanLeadDetailsRecord lead,
        long? contactId,
        CancellationToken cancellationToken)
    {
        if (!contactId.HasValue)
        {
            return;
        }

        var inboxId = lead.Chatwoot.InboxId ?? ResolveInboxId(lead.BoardType);
        if (!TryBuildContactRequest(lead, inboxId, out var contactRequest, out _))
        {
            return;
        }

        await _chatwootApiClient.UpdateContactAsync(contactId.Value, contactRequest!, cancellationToken);
        await SyncManagedLabelsAsync(
            listLabels: token => _chatwootApiClient.ListContactLabelsAsync(contactId.Value, token),
            replaceLabels: (labels, token) => _chatwootApiClient.ReplaceContactLabelsAsync(contactId.Value, labels, token),
            mapping: ChatwootStageMappings.Resolve(lead.BoardType, lead.StageName),
            cancellationToken);
    }

    private async Task AppendStageSyncHistoryMessageAsync(
        AdminKanbanLeadDetailsRecord lead,
        long conversationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _chatwootApiClient.CreateMessageAsync(
                conversationId,
                new ChatwootCreateMessageRequest
                {
                    Content = BuildStageSyncMessage(lead),
                    MessageType = "outgoing",
                    Private = true
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nao foi possivel registrar nota privada da etapa do lead {LeadId} no Chatwoot.", lead.Id);
        }
    }

    private static string FormatConversationStatusLabel(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "open" => "aberta",
            "pending" => "pendente",
            "resolved" => "resolvida",
            "snoozed" => "adiada",
            _ => "atualizada"
        };

    private async Task SyncManagedLabelsAsync(
        Func<CancellationToken, Task<IReadOnlyList<string>>> listLabels,
        Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<string>>> replaceLabels,
        ChatwootStageMapping mapping,
        CancellationToken cancellationToken)
    {
        var existingLabels = await listLabels(cancellationToken);
        var mergedLabels = existingLabels
            .Where(label => !label.StartsWith(ManagedLabelPrefix, StringComparison.OrdinalIgnoreCase))
            .Concat(mapping.Labels)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await replaceLabels(mergedLabels, cancellationToken);
    }

    private static string BuildStageSyncMessage(AdminKanbanLeadDetailsRecord lead)
    {
        var sourceMapping = ChatwootLeadSourceMappings.Resolve(lead.Source);
        var latestMove = lead.History.FirstOrDefault(item =>
            string.Equals(item.EventType, "movido", StringComparison.OrdinalIgnoreCase) &&
            item.ToStageId == lead.StageId);

        var lines = new List<string>
        {
            "Atualizacao de etapa registrada no funil do ConsertaPraMim.",
            $"Lead ID: {lead.Id}",
            $"Funil: {AdminKanbanBoardTypes.GetTitle(lead.BoardType)}"
        };

        if (!string.IsNullOrWhiteSpace(latestMove?.FromStageName))
        {
            lines.Add($"Etapa anterior: {latestMove.FromStageName}");
        }

        lines.Add($"Etapa atual: {lead.StageName}");

        if (!string.IsNullOrWhiteSpace(lead.StatusNote))
        {
            lines.Add($"Status do lead: {TrimTo(lead.StatusNote, 300)}");
        }

        if (sourceMapping is not null)
        {
            lines.Add($"Canal de origem: {sourceMapping.DisplayName}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void TryCompleteActiveRetries(int leadId, IReadOnlyCollection<string> operationTypes)
    {
        try
        {
            _ = _chatwootSyncQueueService.CompleteActiveRetriesForLead(leadId, operationTypes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nao foi possivel concluir itens ativos da fila Chatwoot para o lead {LeadId}.", leadId);
        }
    }

    private bool TryEnqueueRetry(int leadId, string operationType, string reason)
    {
        try
        {
            _chatwootSyncQueueService.EnqueueRetry(leadId, operationType, reason);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nao foi possivel enfileirar retentativa Chatwoot para o lead {LeadId}.", leadId);
            return false;
        }
    }

    private sealed record ResolvedChatwootContact(
        ChatwootContactSummary Contact,
        ChatwootContactInboxSummary ContactInbox);

    private sealed record ResolvedChatwootConversation(
        long ConversationId,
        bool CreatedNewConversation);
}
