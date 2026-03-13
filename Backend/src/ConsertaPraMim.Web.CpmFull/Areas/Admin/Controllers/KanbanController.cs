using AppMobileCPM.Areas.Admin.ViewModels;
using AppMobileCPM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppMobileCPM.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = AdminAuthConstants.AuthenticationScheme)]
[Route("admin/funil")]
public sealed class KanbanController : Controller
{
    private readonly IAdminKanbanService _kanbanService;

    public KanbanController(IAdminKanbanService kanbanService)
    {
        _kanbanService = kanbanService;
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
            history = lead.History.Select(item => new
            {
                id = item.Id,
                eventType = item.EventType,
                fromStageName = item.FromStageName,
                toStageName = item.ToStageName,
                description = item.Description,
                createdAt = item.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            })
        });
    }

    [HttpPost("lead/novo")]
    [ValidateAntiForgeryToken]
    public IActionResult CreateLead([FromBody] AdminKanbanLeadInputModel model)
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

            return Json(new { success = true, leadId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("lead/editar")]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateLead([FromBody] AdminKanbanLeadInputModel model)
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

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("lead/ordem")]
    [ValidateAntiForgeryToken]
    public IActionResult SaveOrder([FromBody] AdminKanbanOrderInputModel model)
    {
        if (!ModelState.IsValid || model.Stages.Count == 0)
        {
            return BadRequest(new { success = false, message = "Ordem invalida para atualizar o funil." });
        }

        try
        {
            var saved = _kanbanService.SaveBoardOrder(new AdminKanbanBoardOrderUpdateRequest
            {
                BoardType = model.BoardType,
                ChangedLeadId = model.ChangedLeadId,
                FromStageId = model.FromStageId,
                ToStageId = model.ToStageId,
                Stages = model.Stages
                    .Select(stage => new AdminKanbanStageOrderUpdateItem
                    {
                        StageId = stage.StageId,
                        LeadIds = stage.LeadIds
                    })
                    .ToList()
            });

            return Json(new { success = saved });
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
}
