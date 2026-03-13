using System.Diagnostics;
using System.Net;
using System.Text;
using AppMobileCPM.Models;
using AppMobileCPM.Services;
using AppMobileCPM.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AppMobileCPM.Controllers;

[Route("")]
public class HomeController : Controller
{
    private readonly IMarketplaceRepository _repository;

    public HomeController(IMarketplaceRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View(new HomePageViewModel
        {
            Categories = _repository.GetCategories()
        });
    }

    [HttpGet("profissionais")]
    public IActionResult Professionals(string? searchTerm)
    {
        return View(new ProfessionalsPageViewModel
        {
            SearchTerm = searchTerm ?? string.Empty,
            Professionals = _repository.GetProfessionals(searchTerm)
        });
    }

    [HttpGet("solicitar-servico")]
    public IActionResult RequestService(string? categoryId, string? description, string? location)
    {
        var form = new RequestServiceInputModel
        {
            Description = description ?? string.Empty,
            Location = location ?? string.Empty
        };
        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            var category = _repository.GetCategoryById(categoryId);
            if (category is not null)
            {
                form.CategoryId = category.Id;
            }
        }

        return View(new RequestServicePageViewModel
        {
            Form = form,
            Categories = _repository.GetCategories(),
            IsSubmitted = string.Equals(TempData["RequestSubmitted"]?.ToString(), "1", StringComparison.Ordinal)
        });
    }

    [HttpPost("solicitar-servico")]
    [ValidateAntiForgeryToken]
    public IActionResult RequestService([Bind(Prefix = "Form")] RequestServiceInputModel form)
    {
        var category = _repository.GetCategoryById(form.CategoryId);
        if (category is null)
        {
            ModelState.AddModelError(nameof(form.CategoryId), "Selecione uma categoria valida.");
        }

        if (!ModelState.IsValid || category is null)
        {
            return View(new RequestServicePageViewModel
            {
                Form = form,
                Categories = _repository.GetCategories(),
                IsSubmitted = false
            });
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

        TempData["RequestSubmitted"] = "1";
        return RedirectToAction(nameof(RequestService));
    }

    [HttpGet("cadastro-profissional")]
    public IActionResult RegisterProfessional()
    {
        return View(new RegisterProfessionalPageViewModel
        {
            Form = new RegisterProfessionalInputModel(),
            ProfessionOptions = _repository.GetProfessionOptions(),
            IsSubmitted = string.Equals(TempData["ProfessionalSubmitted"]?.ToString(), "1", StringComparison.Ordinal)
        });
    }

    [HttpPost("cadastro-profissional")]
    [ValidateAntiForgeryToken]
    public IActionResult RegisterProfessional([Bind(Prefix = "Form")] RegisterProfessionalInputModel form)
    {
        var professionOptions = _repository.GetProfessionOptions();
        if (!professionOptions.Contains(form.Profession, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(form.Profession), "Selecione uma profissao valida da lista.");
        }

        if (!ModelState.IsValid)
        {
            return View(new RegisterProfessionalPageViewModel
            {
                Form = form,
                ProfessionOptions = professionOptions,
                IsSubmitted = false
            });
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

        return RedirectToAction(nameof(RegisterProfessionalSuccess), new { name = form.Name });
    }

    [HttpGet("cadastro-profissional/sucesso")]
    public IActionResult RegisterProfessionalSuccess(string? name)
    {
        return View(new RegisterProfessionalSuccessViewModel
        {
            ProfessionalName = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim()
        });
    }

    [HttpGet("termos-de-uso-profissionais")]
    public IActionResult ProfessionalTerms()
    {
        ViewData["LegalHtml"] = _repository.GetSiteContent("legal.professional.html");
        return View();
    }

    [HttpGet("privacidade")]
    public IActionResult Privacy()
    {
        ViewData["LegalHtml"] = _repository.GetSiteContent("legal.privacy.html");
        return View();
    }

    [HttpGet("suporte")]
    public IActionResult Support()
    {
        return View(new SupportPageViewModel
        {
            Form = new SupportRequestInputModel(),
            CategoryOptions = _repository.GetSupportCategoryOptions(),
            FaqItems = _repository.GetSupportFaqItems(),
            IsSubmitted = string.Equals(TempData["SupportSubmitted"]?.ToString(), "1", StringComparison.Ordinal),
            SubmittedName = TempData["SupportName"]?.ToString() ?? string.Empty
        });
    }

    [HttpPost("suporte")]
    [ValidateAntiForgeryToken]
    public IActionResult Support([Bind(Prefix = "Form")] SupportRequestInputModel form)
    {
        var supportCategoryOptions = _repository.GetSupportCategoryOptions();
        if (!supportCategoryOptions.Contains(form.Category, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(form.Category), "Selecione uma categoria valida.");
        }

        if (!ModelState.IsValid)
        {
            return View(new SupportPageViewModel
            {
                Form = form,
                CategoryOptions = supportCategoryOptions,
                FaqItems = _repository.GetSupportFaqItems(),
                IsSubmitted = false
            });
        }

        _repository.AddSupportRequest(new SupportRequest
        {
            Name = form.Name,
            Email = form.Email,
            Phone = form.Phone,
            Category = form.Category,
            Subject = form.Subject,
            Message = form.Message,
            SubmittedAt = DateTimeOffset.UtcNow
        });

        TempData["SupportSubmitted"] = "1";
        TempData["SupportName"] = form.Name;
        return RedirectToAction(nameof(Support));
    }

    [HttpGet("robots.txt")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public IActionResult Robots()
    {
        var sitemapUrl = $"{Request.Scheme}://{Request.Host}/sitemap.xml";
        var robotsContent = $"""
User-agent: *
Allow: /
Disallow: /cadastro-profissional/sucesso
Sitemap: {sitemapUrl}
""";

        return Content(robotsContent, "text/plain", Encoding.UTF8);
    }

    [HttpGet("sitemap.xml")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public IActionResult Sitemap()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var lastModified = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var pages = new (string Path, string ChangeFrequency, string Priority)[]
        {
            ("/", "daily", "1.0"),
            ("/profissionais", "daily", "0.9"),
            ("/solicitar-servico", "daily", "0.9"),
            ("/cadastro-profissional", "weekly", "0.8"),
            ("/termos-de-uso-profissionais", "monthly", "0.4"),
            ("/privacidade", "monthly", "0.4"),
            ("/suporte", "weekly", "0.7")
        };

        var xml = new StringBuilder();
        xml.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        xml.AppendLine("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""");

        foreach (var page in pages)
        {
            var loc = WebUtility.HtmlEncode($"{baseUrl}{page.Path}");
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{loc}</loc>");
            xml.AppendLine($"    <lastmod>{lastModified}</lastmod>");
            xml.AppendLine($"    <changefreq>{page.ChangeFrequency}</changefreq>");
            xml.AppendLine($"    <priority>{page.Priority}</priority>");
            xml.AppendLine("  </url>");
        }

        xml.AppendLine("</urlset>");
        return Content(xml.ToString(), "application/xml", Encoding.UTF8);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
