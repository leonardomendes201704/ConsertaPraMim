using AppMobileCPM.Areas.Admin.ViewModels;
using AppMobileCPM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppMobileCPM.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = AdminAuthConstants.AuthenticationScheme)]
[Route("admin/faq-suporte")]
public sealed class SupportFaqController : Controller
{
    private readonly IAdminSupportFaqService _faqService;

    public SupportFaqController(IAdminSupportFaqService faqService)
    {
        _faqService = faqService;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var items = _faqService
            .GetAll()
            .Select(item => new AdminSupportFaqListItemViewModel
            {
                Id = item.Id,
                Question = item.Question,
                Answer = item.Answer,
                AnswerPreview = item.Answer.Length <= 120 ? item.Answer : $"{item.Answer[..120]}...",
                IsActive = item.IsActive,
                SortOrder = item.SortOrder,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .ToList();

        return View(new AdminSupportFaqListViewModel
        {
            Items = items
        });
    }

    [HttpGet("{id:int}/json")]
    public IActionResult DetailsJson(int id)
    {
        var item = _faqService.GetById(id);
        if (item is null)
        {
            return NotFound();
        }

        return Json(new
        {
            id = item.Id,
            question = item.Question,
            answer = item.Answer,
            isActive = item.IsActive,
            sortOrder = item.SortOrder,
            createdAt = item.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
            updatedAt = item.UpdatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "-"
        });
    }

    [HttpPost("novo")]
    [ValidateAntiForgeryToken]
    public IActionResult Create(AdminSupportFaqInputModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminErrorMessage"] = "Preencha os campos obrigatorios para criar o FAQ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _faqService.Create(new AdminSupportFaqUpsertRequest
            {
                Question = model.Question,
                Answer = model.Answer,
                IsActive = model.IsActive,
                SortOrder = model.SortOrder
            });
            TempData["AdminSuccessMessage"] = "FAQ criado com sucesso.";
        }
        catch
        {
            TempData["AdminErrorMessage"] = "Nao foi possivel criar o FAQ.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("editar")]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(AdminSupportFaqInputModel model)
    {
        if (!ModelState.IsValid || model.Id <= 0)
        {
            TempData["AdminErrorMessage"] = "Dados invalidos para atualizar o FAQ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var updated = _faqService.Update(model.Id, new AdminSupportFaqUpsertRequest
            {
                Question = model.Question,
                Answer = model.Answer,
                IsActive = model.IsActive,
                SortOrder = model.SortOrder
            });

            TempData["AdminSuccessMessage"] = updated
                ? "FAQ atualizado com sucesso."
                : "FAQ nao encontrado para atualizacao.";
        }
        catch
        {
            TempData["AdminErrorMessage"] = "Nao foi possivel atualizar o FAQ.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("excluir")]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        if (id <= 0)
        {
            TempData["AdminErrorMessage"] = "FAQ invalido para exclusao.";
            return RedirectToAction(nameof(Index));
        }

        var deleted = _faqService.Delete(id);
        TempData["AdminSuccessMessage"] = deleted
            ? "FAQ excluido com sucesso."
            : "FAQ nao encontrado para exclusao.";

        return RedirectToAction(nameof(Index));
    }
}
