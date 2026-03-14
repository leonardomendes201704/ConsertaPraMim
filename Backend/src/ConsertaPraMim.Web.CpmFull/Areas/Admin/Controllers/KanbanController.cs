using AppMobileCPM.Areas.Admin.ViewModels;
using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Integrations.Telegram;
using AppMobileCPM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = AdminAuthConstants.AuthenticationScheme)]
[Route("admin/funil")]
public sealed class KanbanController : Controller
{
    private readonly IAdminKanbanService _kanbanService;
    private readonly IChatwootSyncQueueService _chatwootSyncQueueService;
    private readonly IChatwootLeadSyncService _chatwootLeadSyncService;
    private readonly IChatwootBackfillService _chatwootBackfillService;
    private readonly ITelegramBridgeObservabilityClient _telegramBridgeObservabilityClient;
    private readonly ChatwootOptions _chatwootOptions;
    private readonly TelegramAutomationOptions _telegramAutomationOptions;

    public KanbanController(
        IAdminKanbanService kanbanService,
        IChatwootSyncQueueService chatwootSyncQueueService,
        IChatwootLeadSyncService chatwootLeadSyncService,
        IChatwootBackfillService chatwootBackfillService,
        ITelegramBridgeObservabilityClient telegramBridgeObservabilityClient,
        IOptions<ChatwootOptions> chatwootOptions,
        IOptions<TelegramAutomationOptions> telegramAutomationOptions)
    {
        _kanbanService = kanbanService;
        _chatwootSyncQueueService = chatwootSyncQueueService;
        _chatwootLeadSyncService = chatwootLeadSyncService;
        _chatwootBackfillService = chatwootBackfillService;
        _telegramBridgeObservabilityClient = telegramBridgeObservabilityClient;
        _chatwootOptions = chatwootOptions.Value;
        _telegramAutomationOptions = telegramAutomationOptions.Value;
    }

    [HttpGet("clientes")]
    public IActionResult Clients()
    {
        return View("Index", BuildPageModel(AdminKanbanBoardTypes.Clients));
    }

    [HttpGet("prestadores")]
    public IActionResult Providers()
    {
        return View("Index", BuildPageModel(AdminKanbanBoardTypes.Providers));
    }

    [HttpGet("lead/{id:int}/json")]
    public IActionResult LeadDetailsJson(int id)
    {
        var lead = _kanbanService.GetLeadDetails(id);
        if (lead is null)
        {
            return NotFound();
        }

        return Json(new
        {
            id = lead.Id,
            boardType = lead.BoardType,
            stageId = lead.StageId,
            stageName = lead.StageName,
            name = lead.Name,
            phone = lead.Phone,
            email = lead.Email,
            serviceCategory = lead.ServiceCategory,
            postalCode = lead.PostalCode,
            city = lead.City,
            source = lead.Source,
            priority = lead.Priority,
            statusNote = lead.StatusNote,
            internalNotes = lead.InternalNotes,
            createdAt = lead.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
            updatedAt = lead.UpdatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "-",
            lastContactAt = lead.LastContactAt?.ToString("yyyy-MM-ddTHH:mm") ?? string.Empty,
            telegram = new
            {
                originLabel = string.Equals(lead.Source, "Telegram", StringComparison.OrdinalIgnoreCase) ? "Telegram" : "-",
                chatbotConversationId = lead.Telegram.ChatbotConversationId?.ToString() ?? string.Empty,
                channelConversationId = lead.Telegram.ChannelConversationId,
                telegramChatId = lead.Telegram.TelegramChatId,
                clientId = lead.Telegram.ClientId?.ToString() ?? string.Empty,
                clientEmail = lead.Telegram.ClientEmail,
                serviceRequestId = lead.Telegram.ServiceRequestId?.ToString() ?? string.Empty,
                humanHandoffStartedAt = lead.Telegram.HumanHandoffStartedAt?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                lastTelegramMessageSyncedAt = lead.Telegram.LastTelegramMessageSyncedAt?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                lastChatwootMessageSyncedAt = lead.Telegram.LastChatwootMessageSyncedAt?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                updatedAt = lead.Telegram.UpdatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "-"
            },
            chatwoot = new
            {
                contactId = lead.Chatwoot.ContactId,
                conversationId = lead.Chatwoot.ConversationId,
                inboxId = lead.Chatwoot.InboxId,
                syncStatus = lead.Chatwoot.SyncStatus,
                syncStatusLabel = FormatChatwootSyncStatusLabel(lead.Chatwoot.SyncStatus),
                lastSyncAt = lead.Chatwoot.LastSyncAt?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                lastError = string.IsNullOrWhiteSpace(lead.Chatwoot.LastError) ? "-" : ChatwootSecuritySanitizer.SanitizeMessage(lead.Chatwoot.LastError, 500),
                conversationUrl = BuildChatwootConversationUrl(lead.Chatwoot.ConversationId)
            },
            history = lead.History.Select(item => new
            {
                id = item.Id,
                eventType = item.EventType,
                eventTypeLabel = FormatHistoryEventLabel(item.EventType),
                fromStageName = item.FromStageName,
                toStageName = item.ToStageName,
                description = item.Description,
                createdAt = item.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            })
        });
    }

    [HttpPost("lead/novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLead([FromBody] AdminKanbanLeadInputModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = "Dados invalidos para criar o lead." });
        }

        try
        {
            var leadId = _kanbanService.CreateLead(new AdminKanbanLeadUpsertRequest
            {
                BoardType = model.BoardType,
                StageId = model.StageId,
                Name = model.Name,
                Phone = model.Phone,
                Email = model.Email,
                ServiceCategory = model.ServiceCategory,
                PostalCode = model.PostalCode,
                City = model.City,
                Source = model.Source,
                Priority = model.Priority,
                StatusNote = model.StatusNote,
                InternalNotes = model.InternalNotes,
                LastContactAt = model.LastContactAt
            });

            var syncResult = await _chatwootLeadSyncService.SyncLeadAsync(leadId, HttpContext.RequestAborted);
            return Json(new
            {
                success = true,
                leadId,
                chatwoot = new
                {
                    status = syncResult.Status,
                    message = syncResult.Message
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("lead/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateLead([FromBody] AdminKanbanLeadInputModel model)
    {
        if (!ModelState.IsValid || model.Id <= 0)
        {
            return BadRequest(new { success = false, message = "Dados invalidos para atualizar o lead." });
        }

        try
        {
            var updated = _kanbanService.UpdateLead(model.Id, new AdminKanbanLeadUpsertRequest
            {
                BoardType = model.BoardType,
                StageId = model.StageId,
                Name = model.Name,
                Phone = model.Phone,
                Email = model.Email,
                ServiceCategory = model.ServiceCategory,
                PostalCode = model.PostalCode,
                City = model.City,
                Source = model.Source,
                Priority = model.Priority,
                StatusNote = model.StatusNote,
                InternalNotes = model.InternalNotes,
                LastContactAt = model.LastContactAt
            });

            if (!updated)
            {
                return NotFound(new { success = false, message = "Lead nao encontrado para atualizacao." });
            }

            var syncResult = await _chatwootLeadSyncService.SyncLeadAsync(model.Id, HttpContext.RequestAborted);
            return Json(new
            {
                success = true,
                chatwoot = new
                {
                    status = syncResult.Status,
                    message = syncResult.Message
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("lead/{id:int}/chatwoot/sincronizar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncLeadChatwoot(int id)
    {
        var result = await _chatwootLeadSyncService.SyncLeadAsync(id, HttpContext.RequestAborted);
        if (result.Status == ChatwootSyncStatuses.NotFound)
        {
            return NotFound(new { success = false, message = result.Message });
        }

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                success = false,
                status = result.Status,
                message = result.Message
            });
        }

        return Json(new
        {
            success = true,
            status = result.Status,
            message = result.Message,
            contactId = result.ContactId,
            conversationId = result.ConversationId,
            inboxId = result.InboxId
        });
    }

    [HttpPost("lead/{id:int}/chatwoot/retentativa")]
    [ValidateAntiForgeryToken]
    public IActionResult EnqueueLeadChatwootRetry(int id)
    {
        if (!_chatwootOptions.Enabled)
        {
            return BadRequest(new
            {
                success = false,
                message = "Integracao com Chatwoot desabilitada no ambiente atual."
            });
        }

        var lead = _kanbanService.GetLeadDetails(id);
        if (lead is null)
        {
            return NotFound(new { success = false, message = "Lead nao encontrado para retentativa do Chatwoot." });
        }

        try
        {
            var operationType = _chatwootSyncQueueService.ResolveOperationType(lead);
            _chatwootSyncQueueService.EnqueueRetry(
                id,
                operationType,
                "Retentativa manual solicitada no painel do funil.",
                runImmediately: true);

            var operationLabel = operationType == ChatwootSyncOperationTypes.StageSync
                ? "Retentativa de sincronizacao da etapa enfileirada para processamento imediato."
                : "Retentativa de sincronizacao do lead enfileirada para processamento imediato.";

            return Json(new
            {
                success = true,
                status = ChatwootSyncQueueStatuses.Queued,
                message = operationLabel
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("chatwoot/backfill")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunChatwootBackfill([FromBody] AdminKanbanChatwootBackfillInputModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = "Parametros invalidos para executar o backfill do Chatwoot." });
        }

        if (!string.IsNullOrWhiteSpace(model.BoardType) && !AdminKanbanBoardTypes.IsValid(model.BoardType))
        {
            return BadRequest(new { success = false, message = "Tipo de funil invalido para o backfill do Chatwoot." });
        }

        try
        {
            var result = await _chatwootBackfillService.RunAsync(
                new ChatwootBackfillRunRequest
                {
                    BoardType = string.IsNullOrWhiteSpace(model.BoardType) ? null : model.BoardType,
                    BatchSize = model.BatchSize,
                    DryRun = model.DryRun,
                    StartAfterLeadId = model.StartAfterLeadId
                },
                HttpContext.RequestAborted);

            return Json(new
            {
                success = true,
                dryRun = result.DryRun,
                status = result.Status,
                statusLabel = FormatBackfillRunStatusLabel(result.Status),
                scopeKey = result.ScopeKey,
                scopeLabel = result.ScopeLabel,
                batchSize = result.BatchSize,
                storedCheckpointLeadId = result.StoredCheckpointLeadId,
                effectiveStartAfterLeadId = result.EffectiveStartAfterLeadId,
                lastProcessedLeadId = result.LastProcessedLeadId,
                summary = new
                {
                    totalSelected = result.TotalSelected,
                    successCount = result.SuccessCount,
                    failedCount = result.FailedCount,
                    pendingCount = result.PendingCount
                },
                items = result.Items.Select(item => new
                {
                    leadId = item.LeadId,
                    boardType = item.BoardType,
                    boardLabel = AdminKanbanBoardTypes.GetTitle(item.BoardType),
                    leadName = item.LeadName,
                    stageName = item.StageName,
                    status = item.Status,
                    statusLabel = FormatBackfillItemStatusLabel(item.Status),
                    message = item.Message,
                    contactId = item.ContactId,
                    conversationId = item.ConversationId,
                    inboxId = item.InboxId
                })
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("chatwoot/diagnostico/json")]
    public IActionResult ChatwootDiagnosticsJson([FromQuery] string? boardType, [FromQuery] int issueLimit = 10, [FromQuery] int queueLimit = 10)
    {
        if (!string.IsNullOrWhiteSpace(boardType) && !AdminKanbanBoardTypes.IsValid(boardType))
        {
            return BadRequest(new { success = false, message = "Tipo de funil invalido para diagnostico do Chatwoot." });
        }

        var requestedBoardType = string.IsNullOrWhiteSpace(boardType)
            ? null
            : AdminKanbanBoardTypes.Normalize(boardType);
        var diagnostics = _kanbanService.GetChatwootDiagnostics(requestedBoardType, issueLimit, queueLimit);
        var effectiveBoardType = string.IsNullOrWhiteSpace(diagnostics.ScopeBoardType)
            ? string.Empty
            : diagnostics.ScopeBoardType;

        return Json(new
        {
            success = true,
            enabled = _chatwootOptions.Enabled,
            scope = new
            {
                boardType = effectiveBoardType,
                boardLabel = FormatDiagnosticsScopeLabel(effectiveBoardType)
            },
            summary = new
            {
                totalLeads = diagnostics.TotalLeads,
                syncedCount = diagnostics.SyncedCount,
                pendingCount = diagnostics.PendingCount,
                failedCount = diagnostics.FailedCount,
                activeQueueCount = diagnostics.ActiveQueueCount,
                deadLetterCount = diagnostics.DeadLetterCount
            },
            recentIssues = diagnostics.RecentIssues.Select(item => new
            {
                leadId = item.LeadId,
                boardType = item.BoardType,
                boardLabel = AdminKanbanBoardTypes.GetTitle(item.BoardType),
                leadName = item.LeadName,
                stageName = item.StageName,
                syncStatus = item.SyncStatus,
                syncStatusLabel = FormatChatwootSyncStatusLabel(item.SyncStatus),
                lastSyncAt = item.LastSyncAt?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                lastError = string.IsNullOrWhiteSpace(item.LastError) ? "-" : ChatwootSecuritySanitizer.SanitizeMessage(item.LastError, 500),
                contactId = item.ContactId,
                conversationId = item.ConversationId,
                inboxId = item.InboxId,
                conversationUrl = BuildChatwootConversationUrl(item.ConversationId)
            }),
            recentQueueItems = diagnostics.RecentQueueItems.Select(item => new
            {
                queueItemId = item.QueueItemId,
                leadId = item.LeadId,
                boardType = item.BoardType,
                boardLabel = AdminKanbanBoardTypes.GetTitle(item.BoardType),
                leadName = item.LeadName,
                stageName = item.StageName,
                operationType = item.OperationType,
                operationLabel = FormatQueueOperationLabel(item.OperationType),
                status = item.Status,
                statusLabel = FormatQueueStatusLabel(item.Status),
                attemptCount = item.AttemptCount,
                maxAttempts = item.MaxAttempts,
                nextAttemptAt = item.NextAttemptAt.ToString("dd/MM/yyyy HH:mm"),
                lastAttemptAt = item.LastAttemptAt?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                lastError = string.IsNullOrWhiteSpace(item.LastError) ? "-" : ChatwootSecuritySanitizer.SanitizeMessage(item.LastError, 500),
                conversationId = item.ConversationId,
                conversationUrl = BuildChatwootConversationUrl(item.ConversationId)
            })
        });
    }

    [HttpGet("telegram/diagnostico/json")]
    public async Task<IActionResult> TelegramDiagnosticsJson([FromQuery] string? boardType, [FromQuery] int issueLimit = 10, [FromQuery] int queueLimit = 10)
    {
        if (!string.IsNullOrWhiteSpace(boardType) && !AdminKanbanBoardTypes.IsValid(boardType))
        {
            return BadRequest(new { success = false, message = "Tipo de funil invalido para diagnostico do Telegram." });
        }

        var requestedBoardType = string.IsNullOrWhiteSpace(boardType)
            ? null
            : AdminKanbanBoardTypes.Normalize(boardType);
        var diagnostics = _kanbanService.GetTelegramDiagnostics(requestedBoardType, issueLimit, queueLimit);
        var effectiveBoardType = string.IsNullOrWhiteSpace(diagnostics.ScopeBoardType)
            ? string.Empty
            : diagnostics.ScopeBoardType;

        TelegramBridgeObservabilitySnapshotDto? bridgeSnapshot = null;
        string bridgeMessage;
        if (_telegramAutomationOptions.Enabled)
        {
            var bridgeResult = await _telegramBridgeObservabilityClient.GetSnapshotAsync(HttpContext.RequestAborted);
            bridgeSnapshot = bridgeResult.Success ? bridgeResult.Snapshot : null;
            bridgeMessage = bridgeResult.Message;
        }
        else
        {
            bridgeMessage = "Automacao Telegram desabilitada no ambiente atual.";
        }

        return Json(new
        {
            success = true,
            enabled = _telegramAutomationOptions.Enabled,
            mirrorMessagesEnabled = _telegramAutomationOptions.MirrorMessagesEnabled,
            scope = new
            {
                boardType = effectiveBoardType,
                boardLabel = FormatDiagnosticsScopeLabel(effectiveBoardType)
            },
            summary = new
            {
                totalTelegramLeads = diagnostics.TotalTelegramLeads,
                leadsWithInboundMirror = diagnostics.LeadsWithInboundMirror,
                leadsWithOutboundMirror = diagnostics.LeadsWithOutboundMirror,
                humanHandoffCount = diagnostics.HumanHandoffCount,
                activeQueueCount = diagnostics.ActiveQueueCount,
                deadLetterCount = diagnostics.DeadLetterCount
            },
            recentIssues = diagnostics.RecentIssues.Select(item => new
            {
                queueItemId = item.QueueItemId,
                leadId = item.LeadId,
                boardType = item.BoardType,
                boardLabel = AdminKanbanBoardTypes.GetTitle(item.BoardType),
                leadName = item.LeadName,
                stageName = item.StageName,
                direction = item.Direction,
                directionLabel = FormatTelegramDirectionLabel(item.Direction),
                status = item.Status,
                statusLabel = FormatTelegramQueueStatusLabel(item.Status),
                attemptCount = item.AttemptCount,
                maxAttempts = item.MaxAttempts,
                lastAttemptAt = item.LastAttemptAt?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                lastError = string.IsNullOrWhiteSpace(item.LastError) ? "-" : ChatwootSecuritySanitizer.SanitizeMessage(item.LastError, 500),
                chatwootConversationId = item.ChatwootConversationId,
                conversationUrl = BuildChatwootConversationUrl(item.ChatwootConversationId),
                telegramChatId = item.TelegramChatId
            }),
            recentQueueItems = diagnostics.RecentQueueItems.Select(item => new
            {
                queueItemId = item.QueueItemId,
                leadId = item.LeadId,
                boardType = item.BoardType,
                boardLabel = AdminKanbanBoardTypes.GetTitle(item.BoardType),
                leadName = item.LeadName,
                stageName = item.StageName,
                direction = item.Direction,
                directionLabel = FormatTelegramDirectionLabel(item.Direction),
                status = item.Status,
                statusLabel = FormatTelegramQueueStatusLabel(item.Status),
                attemptCount = item.AttemptCount,
                maxAttempts = item.MaxAttempts,
                nextAttemptAt = item.NextAttemptAt.ToString("dd/MM/yyyy HH:mm"),
                lastAttemptAt = item.LastAttemptAt?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                lastError = string.IsNullOrWhiteSpace(item.LastError) ? "-" : ChatwootSecuritySanitizer.SanitizeMessage(item.LastError, 500),
                chatwootConversationId = item.ChatwootConversationId,
                conversationUrl = BuildChatwootConversationUrl(item.ChatwootConversationId),
                telegramChatId = item.TelegramChatId
            }),
            bridge = new
            {
                available = bridgeSnapshot is not null,
                message = bridgeMessage,
                generatedAt = bridgeSnapshot?.GeneratedAtUtc.ToString("dd/MM/yyyy HH:mm") ?? "-",
                environment = bridgeSnapshot?.Environment ?? string.Empty,
                traffic = new
                {
                    inboundMessages = bridgeSnapshot?.Traffic.InboundMessages ?? 0,
                    outboundMessages = bridgeSnapshot?.Traffic.OutboundMessages ?? 0,
                    messagesWithAttachments = bridgeSnapshot?.Traffic.MessagesWithAttachments ?? 0
                },
                ai = new
                {
                    requests = bridgeSnapshot?.Ai.Requests ?? 0,
                    failures = bridgeSnapshot?.Ai.Failures ?? 0,
                    fallbacks = bridgeSnapshot?.Ai.Fallbacks ?? 0,
                    humanHandoffs = bridgeSnapshot?.Ai.HumanHandoffs ?? 0,
                    avgLatencyMs = bridgeSnapshot?.Ai.AvgLatencyMs ?? 0,
                    p95LatencyMs = bridgeSnapshot?.Ai.P95LatencyMs ?? 0
                },
                business = new
                {
                    triageRequestsOpened = bridgeSnapshot?.Business.TriageRequestsOpened ?? 0,
                    schedulingAttempts = bridgeSnapshot?.Business.SchedulingAttempts ?? 0,
                    schedulingConfirmed = bridgeSnapshot?.Business.SchedulingConfirmed ?? 0,
                    schedulingFailures = bridgeSnapshot?.Business.SchedulingFailures ?? 0,
                    queryRequests = bridgeSnapshot?.Business.QueryRequests ?? 0
                },
                dependencies = bridgeSnapshot?.Dependencies?.Select(item => new
                {
                    dependency = item.Dependency,
                    calls = item.Calls,
                    successes = item.Successes,
                    failures = item.Failures,
                    avgLatencyMs = item.AvgLatencyMs,
                    p95LatencyMs = item.P95LatencyMs
                }),
                topErrors = bridgeSnapshot?.TopErrors?.Select(item => new
                {
                    errorCode = item.ErrorCode,
                    count = item.Count
                }),
                recentIncidents = bridgeSnapshot?.RecentIncidents?
                    .Take(Math.Clamp(issueLimit, 1, 100))
                    .Select(item => new
                    {
                        occurredAt = item.OccurredAtUtc.ToString("dd/MM/yyyy HH:mm"),
                        stage = item.Stage,
                        errorCode = item.ErrorCode,
                        correlationId = item.CorrelationId ?? "-",
                        message = string.IsNullOrWhiteSpace(item.Message) ? "-" : ChatwootSecuritySanitizer.SanitizeMessage(item.Message, 500)
                    })
            }
        });
    }

    [HttpPost("telegram/fila/{queueItemId:int}/retentativa")]
    [ValidateAntiForgeryToken]
    public IActionResult RequeueTelegramQueueItem(int queueItemId)
    {
        if (!_telegramAutomationOptions.Enabled || !_telegramAutomationOptions.MirrorMessagesEnabled)
        {
            return BadRequest(new
            {
                success = false,
                message = "Automacao Telegram desabilitada no ambiente atual."
            });
        }

        var queueItem = _kanbanService.RequeueTelegramDeliveryQueueItem(queueItemId, DateTime.UtcNow, "admin-manual");
        if (queueItem is null)
        {
            return NotFound(new { success = false, message = "Item da fila Telegram nao encontrado para retentativa." });
        }

        _ = _kanbanService.AddHistoryEvent(
            queueItem.LeadId,
            "telegram_entrega_enfileirada",
            $"Retentativa manual enfileirada para {FormatTelegramDirectionLabel(queueItem.Direction)}.");

        return Json(new
        {
            success = true,
            queueItemId = queueItem.Id,
            leadId = queueItem.LeadId,
            status = queueItem.Status,
            statusLabel = FormatTelegramQueueStatusLabel(queueItem.Status),
            message = "Retentativa Telegram enfileirada para processamento imediato."
        });
    }

    [HttpPost("lead/ordem")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOrder([FromBody] AdminKanbanOrderInputModel model)
    {
        if (!ModelState.IsValid || model.Stages.Count == 0)
        {
            return BadRequest(new { success = false, message = "Ordem invalida para atualizar o funil." });
        }

        try
        {
            var changedLeadId = model.ChangedLeadId > 0 ? model.ChangedLeadId : (int?)null;
            var fromStageId = model.FromStageId > 0 ? model.FromStageId : (int?)null;
            var toStageId = model.ToStageId > 0 ? model.ToStageId : (int?)null;

            var saved = _kanbanService.SaveBoardOrder(new AdminKanbanBoardOrderUpdateRequest
            {
                BoardType = model.BoardType,
                ChangedLeadId = changedLeadId,
                FromStageId = fromStageId,
                ToStageId = toStageId,
                Stages = model.Stages
                    .Select(stage => new AdminKanbanStageOrderUpdateItem
                    {
                        StageId = stage.StageId,
                        LeadIds = stage.LeadIds
                    })
                    .ToList()
            });

            ChatwootLeadSyncResult? chatwoot = null;
            if (saved && changedLeadId.HasValue && fromStageId.HasValue && toStageId.HasValue && fromStageId.Value != toStageId.Value)
            {
                chatwoot = await _chatwootLeadSyncService.SyncLeadStageAsync(changedLeadId.Value, HttpContext.RequestAborted);
            }

            return Json(new
            {
                success = saved,
                chatwoot = chatwoot is null
                    ? null
                    : new
                    {
                        status = chatwoot.Status,
                        message = chatwoot.Message
                    }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("lead/nota")]
    [ValidateAntiForgeryToken]
    public IActionResult AddNote([FromBody] AdminKanbanLeadNoteInputModel model)
    {
        if (!ModelState.IsValid || model.LeadId <= 0)
        {
            return BadRequest(new { success = false, message = "Anotacao invalida." });
        }

        var added = _kanbanService.AddHistoryNote(model.LeadId, model.Note);
        if (!added)
        {
            return BadRequest(new { success = false, message = "Nao foi possivel registrar a anotacao." });
        }

        return Json(new { success = true });
    }

    private AdminKanbanPageViewModel BuildPageModel(string boardType)
    {
        var normalizedBoardType = AdminKanbanBoardTypes.Normalize(boardType);
        var board = _kanbanService.GetBoard(normalizedBoardType);

        var alternateBoardType = normalizedBoardType == AdminKanbanBoardTypes.Clients
            ? AdminKanbanBoardTypes.Providers
            : AdminKanbanBoardTypes.Clients;

        var alternateBoardUrl = alternateBoardType == AdminKanbanBoardTypes.Clients
            ? Url.Action(nameof(Clients), "Kanban", new { area = "Admin" }) ?? "/admin/funil/clientes"
            : Url.Action(nameof(Providers), "Kanban", new { area = "Admin" }) ?? "/admin/funil/prestadores";

        return new AdminKanbanPageViewModel
        {
            BoardType = normalizedBoardType,
            BoardTitle = AdminKanbanBoardTypes.GetTitle(normalizedBoardType),
            BoardSubtitle = AdminKanbanBoardTypes.GetSubtitle(normalizedBoardType),
            AlternateBoardUrl = alternateBoardUrl,
            AlternateBoardLabel = AdminKanbanBoardTypes.GetTitle(alternateBoardType),
            Stages = board.Stages.Select(stage => new AdminKanbanStageViewModel
            {
                Id = stage.Id,
                Name = stage.Name,
                Color = stage.Color,
                SortOrder = stage.SortOrder,
                Leads = stage.Leads.Select(lead => new AdminKanbanLeadCardViewModel
                {
                    Id = lead.Id,
                    StageId = lead.StageId,
                    Name = lead.Name,
                    Phone = lead.Phone,
                    Email = lead.Email,
                    ServiceCategory = lead.ServiceCategory,
                    Source = lead.Source,
                    Priority = lead.Priority,
                    StatusNote = lead.StatusNote,
                    ChatwootSyncStatus = lead.ChatwootSyncStatus,
                    StageEnteredAt = lead.StageEnteredAt,
                    CreatedAt = lead.CreatedAt,
                    UpdatedAt = lead.UpdatedAt,
                    LastContactAt = lead.LastContactAt
                }).ToList()
            }).ToList()
        };
    }

    private static string FormatChatwootSyncStatusLabel(string? syncStatus) =>
        (syncStatus ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "pending" => "Pendente",
            "synced" => "Sincronizado",
            "failed" => "Falha",
            "skipped" => "Ignorado",
            "disabled" => "Desabilitado",
            _ => "Ainda nao sincronizado"
        };

    private static string FormatHistoryEventLabel(string? eventType) =>
        (eventType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "criado" => "Lead criado",
            "movido" => "Etapa alterada",
            "atualizado" => "Dados atualizados",
            "nota" => "Anotacao",
            "seed" => "Carga inicial",
            "telegram_lead_criado" => "Lead criado via bot Telegram",
            "telegram_lead_atualizado" => "Lead atualizado via bot Telegram",
            "telegram_entrega_enfileirada" => "Entrega Telegram enfileirada",
            "telegram_message_synced_to_chatwoot" => "Mensagem Telegram espelhada no Chatwoot",
            "chatwoot_handoff_humano_iniciado" => "Handoff humano iniciado no Chatwoot",
            "chatwoot_message_synced_to_telegram" => "Mensagem humana enviada ao Telegram",
            "telegram_dead_letter" => "Entrega Telegram esgotada",
            "chatwoot_bootstrap_via_telegram" => "Bootstrap Telegram no Chatwoot",
            "chatwoot_contato_sincronizado" => "Contato sincronizado no Chatwoot",
            "chatwoot_conversa_criada" => "Conversa criada no Chatwoot",
            "chatwoot_conversa_reaproveitada" => "Conversa reaproveitada no Chatwoot",
            "chatwoot_sincronizado" => "Sincronizacao com Chatwoot",
            "chatwoot_sync_falhou" => "Falha na sincronizacao com Chatwoot",
            "chatwoot_etapa_sincronizada" => "Etapa sincronizada no Chatwoot",
            "chatwoot_etapa_sync_falhou" => "Falha ao sincronizar etapa no Chatwoot",
            "chatwoot_mensagem_recebida" => "Mensagem recebida no Chatwoot",
            "chatwoot_resposta_enviada" => "Resposta enviada no Chatwoot",
            "chatwoot_status_alterado" => "Status alterado no Chatwoot",
            "chatwoot_conversa_atualizada" => "Conversa atualizada no Chatwoot",
            "chatwoot_retentativa_enfileirada" => "Retentativa Chatwoot enfileirada",
            "chatwoot_retentativa_processada" => "Retentativa Chatwoot concluida",
            "chatwoot_dead_letter" => "Retentativa Chatwoot esgotada",
            _ => "Evento do funil"
        };

    private static string FormatBackfillRunStatusLabel(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            ChatwootBackfillRunStatuses.DryRun => "Dry-run",
            ChatwootBackfillRunStatuses.Completed => "Concluido",
            _ => "Backfill"
        };

    private static string FormatBackfillItemStatusLabel(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            ChatwootBackfillItemStatuses.Synced => "Sincronizado",
            ChatwootBackfillItemStatuses.Pending => "Pendente",
            ChatwootBackfillItemStatuses.Skipped => "Ignorado",
            _ => "Falha"
        };

    private static string FormatQueueStatusLabel(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            ChatwootSyncQueueStatuses.Queued => "Na fila",
            ChatwootSyncQueueStatuses.Processing => "Processando",
            ChatwootSyncQueueStatuses.Retrying => "Aguardando retentativa",
            ChatwootSyncQueueStatuses.Processed => "Processado",
            ChatwootSyncQueueStatuses.DeadLetter => "Esgotado",
            _ => "Fila Chatwoot"
        };

    private static string FormatQueueOperationLabel(string? operationType) =>
        (operationType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            ChatwootSyncOperationTypes.LeadSync => "Sincronizacao do lead",
            ChatwootSyncOperationTypes.StageSync => "Sincronizacao da etapa",
            _ => "Operacao Chatwoot"
        };

    private static string FormatTelegramQueueStatusLabel(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            TelegramDeliveryQueueStatuses.Queued => "Na fila",
            TelegramDeliveryQueueStatuses.Processing => "Processando",
            TelegramDeliveryQueueStatuses.Retrying => "Aguardando retentativa",
            TelegramDeliveryQueueStatuses.Processed => "Processado",
            TelegramDeliveryQueueStatuses.DeadLetter => "Esgotado",
            _ => "Fila Telegram"
        };

    private static string FormatTelegramDirectionLabel(string? direction) =>
        (direction ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            TelegramDeliveryDirections.TelegramToChatwoot => "Telegram -> Chatwoot",
            TelegramDeliveryDirections.ChatwootToTelegram => "Chatwoot -> Telegram",
            _ => "Telegram"
        };

    private static string FormatDiagnosticsScopeLabel(string? boardType)
    {
        return string.IsNullOrWhiteSpace(boardType)
            ? "Clientes e prestadores"
            : AdminKanbanBoardTypes.GetTitle(boardType);
    }

    private string BuildChatwootConversationUrl(long? conversationId)
    {
        if (!_chatwootOptions.Enabled || !conversationId.HasValue || string.IsNullOrWhiteSpace(_chatwootOptions.BaseUrl))
        {
            return string.Empty;
        }

        var baseUrl = _chatwootOptions.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/app/accounts/{_chatwootOptions.AccountId}/conversations/{conversationId.Value}";
    }
}
