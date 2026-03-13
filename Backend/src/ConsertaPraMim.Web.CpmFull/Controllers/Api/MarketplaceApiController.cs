using AppMobileCPM.Models;
using AppMobileCPM.Services;
using AppMobileCPM.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AppMobileCPM.Controllers.Api;

[ApiController]
[Route("api")]
public sealed class MarketplaceApiController : ControllerBase
{
    private readonly IMarketplaceRepository _repository;

    public MarketplaceApiController(IMarketplaceRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("categorias")]
    public ActionResult<IReadOnlyList<ServiceCategory>> GetCategories()
    {
        return Ok(_repository.GetCategories());
    }

    [HttpGet("profissionais")]
    public ActionResult<IReadOnlyList<Professional>> GetProfessionals([FromQuery] string? searchTerm = null)
    {
        return Ok(_repository.GetProfessionals(searchTerm));
    }

    [HttpPost("solicitacoes")]
    public IActionResult CreateServiceRequest([FromBody] RequestServiceInputModel form)
    {
        var category = _repository.GetCategoryById(form.CategoryId);
        if (category is null)
        {
            ModelState.AddModelError(nameof(form.CategoryId), "Selecione uma categoria valida.");
            return ValidationProblem(ModelState);
        }

        if (!TryValidateModel(form))
        {
            return ValidationProblem(ModelState);
        }

        _repository.AddServiceRequest(new ServiceRequest
        {
            CategoryId = category.Id,
            CategoryName = category.Name,
            Description = form.Description,
            Location = form.Location,
            Name = form.Name,
            Phone = form.Phone,
            IsWhatsapp = form.IsWhatsapp,
            SubmittedAt = DateTimeOffset.UtcNow
        });

        return Created(string.Empty, new { message = "Solicitacao registrada com sucesso." });
    }

    [HttpPost("cadastros-profissionais")]
    public IActionResult CreateProfessionalRegistration([FromBody] RegisterProfessionalInputModel form)
    {
        if (!TryValidateModel(form))
        {
            return ValidationProblem(ModelState);
        }

        if (!_repository.GetProfessionOptions().Contains(form.Profession, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(form.Profession), "Selecione uma profissao valida da lista.");
            return ValidationProblem(ModelState);
        }

        _repository.AddProfessionalRegistration(new ProfessionalRegistration
        {
            Name = form.Name,
            Profession = form.Profession,
            Services = form.Services,
            PostalCode = form.PostalCode,
            Phone = form.Phone,
            IsWhatsapp = form.IsWhatsapp,
            Experience = form.Experience,
            SubmittedAt = DateTimeOffset.UtcNow
        });

        return Created(string.Empty, new { message = "Cadastro profissional registrado com sucesso." });
    }
}
