using AppMobileCPM.Areas.Admin.ViewModels;
using AppMobileCPM.Integrations.Chatwoot;
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
    private readonly ChatwootOptions _chatwootOptions;

    public KanbanController(
        IAdminKanbanService kanbanService,
        IChatwootSyncQueueService chatwootSyncQueueService,
        IChatwootLeadSyncService chatwootLeadSyncService,
        IOptions<ChatwootOptions> chatwootOptions)
    {
        _kanbanService = kanbanService;
        _chatwootSyncQueueService = chatwootSyncQueueService;
        _chatwootLeadSyncService = chatwootLeadSyncService;
        _chatwootOptions = chatwootOptions.Value;
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
            chatwoot = new
            {
                contactId = lead.Chatwoot.ContactId,
                conversationId = lead.Chatwoot.ConversationId,
                inboxId = lead.Chatwoot.InboxId,
                syncStatus = lead.Chatwoot.SyncStatus,
                syncStatusLabel = FormatChatwootSyncStatusLabel(lead.Chatwoot.SyncStatus),
                lastSyncAt = lead.Chatwoot.LastSyncAt?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                lastError = string.IsNullOrWhiteSpace(lead.Chatwoot.LastError) ? "-" : lead.Chatwoot.LastError,
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
            "chatwoot_contato_sincronizado" => "Contato sincronizado no Chatwoot",
            "chatwoot_conversa_criada" => "Conversa criada no Chatwoot",
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
