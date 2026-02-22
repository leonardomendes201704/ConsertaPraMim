using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Admin.Models;

public class AdminLegalTermsPageViewModel
{
    public string SelectedAudience { get; set; } = "client";
    public string SelectedAudienceLabel { get; set; } = "Cliente";
    public LegalTermsDocumentDto? ActiveDocument { get; set; }
    public IReadOnlyList<LegalTermsDocumentDto> Versions { get; set; } = [];
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public AdminLegalTermsPublishWebRequest PublishRequest { get; set; } = new();
}

public class AdminLegalTermsPublishWebRequest
{
    public string Audience { get; set; } = "client";
    public string Title { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public string? ChangeSummary { get; set; }
}
