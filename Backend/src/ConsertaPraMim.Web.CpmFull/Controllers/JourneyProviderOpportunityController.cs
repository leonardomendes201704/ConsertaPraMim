using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppMobileCPM.Controllers;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("prestadores/oportunidades")]
public sealed class JourneyProviderOpportunityController : Controller
{
    private static readonly byte[] TrackingPixelBytes =
    [
        71, 73, 70, 56, 57, 97, 1, 0, 1, 0, 128, 0, 0, 255, 255, 255,
        0, 0, 0, 33, 249, 4, 1, 0, 0, 1, 0, 44, 0, 0, 0, 0,
        1, 0, 1, 0, 0, 2, 2, 68, 1, 0, 59
    ];

    private readonly IJourneyProviderOpportunityService _opportunityService;

    public JourneyProviderOpportunityController(IJourneyProviderOpportunityService opportunityService)
    {
        _opportunityService = opportunityService;
    }

    [HttpGet("responder")]
    public IActionResult Respond([FromQuery] string token, [FromQuery(Name = "acao")] string action)
    {
        var context = _opportunityService.GetOpportunityContext(token, action, DateTime.UtcNow);
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return View(BuildViewModel(context));
    }

    [HttpPost("responder")]
    public IActionResult Confirm([FromForm] string token, [FromForm(Name = "acao")] string action)
    {
        var result = _opportunityService.ConfirmAction(token, action, DateTime.UtcNow);
        var model = BuildViewModel(result.Context) with
        {
            ActionCompleted = true,
            ActionSucceeded = result.Success,
            FeedbackMessage = result.Message
        };

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return View("Respond", model);
    }

    [HttpGet("rastreio-abertura")]
    public IActionResult TrackOpen([FromQuery] string token)
    {
        _ = _opportunityService.TrackOpen(token, DateTime.UtcNow);
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return File(TrackingPixelBytes, "image/gif");
    }

    private static JourneyProviderOpportunityPageViewModel BuildViewModel(JourneyProviderOpportunityContext context)
    {
        return new JourneyProviderOpportunityPageViewModel
        {
            Title = "Resposta da oportunidade",
            Token = context.ResponseToken,
            Action = context.NormalizedAction,
            ActionLabel = JourneyProviderOpportunityActions.GetLabel(context.NormalizedAction),
            ConfirmationLabel = JourneyProviderOpportunityActions.GetConfirmationLabel(context.NormalizedAction),
            LeadName = context.LeadName,
            ProviderName = context.ProviderName,
            RequestedCategory = context.RequestedCategory,
            RequestedSubcategory = context.RequestedSubcategory,
            QualificationSummary = context.QualificationSummary,
            AddressSummary = context.AddressSummary,
            ScheduledWindowLabel = context.ScheduledWindowLabel,
            DispatchStatusLabel = context.DispatchStatusLabel,
            TargetStatusLabel = context.TargetStatusLabel,
            ResponseHeadline = context.ResponseHeadline,
            ResponseDescription = context.ResponseDescription,
            PortalUrl = context.PortalUrl,
            CanRespond = context.CanRespond,
            AlreadyReserved = context.AlreadyReserved,
            AlreadyResponded = context.AlreadyResponded,
            TokenExpired = context.TokenExpired,
            NotFound = context.NotFound
        };
    }
}
