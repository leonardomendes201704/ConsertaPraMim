using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppMobileCPM.Controllers;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("jornada")]
public sealed class JourneyServiceClosureController : Controller
{
    private readonly IJourneyServiceClosureService _closureService;
    private readonly IJourneyServiceClosureLinkService _linkService;

    public JourneyServiceClosureController(
        IJourneyServiceClosureService closureService,
        IJourneyServiceClosureLinkService linkService)
    {
        _closureService = closureService;
        _linkService = linkService;
    }

    [HttpGet("encerramento/prestador")]
    public IActionResult ProviderCompletion([FromQuery] string token)
    {
        var context = _closureService.GetProviderCompletionContext(token, DateTime.UtcNow);
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return View("ProviderCompletion", BuildCompletionModel(context, token, JourneyServiceClosureAudiences.Provider));
    }

    [HttpPost("encerramento/prestador")]
    public async Task<IActionResult> ProviderCompletionSubmit([FromForm] string token, [FromForm] string outcome, [FromForm] string notes, CancellationToken cancellationToken)
    {
        var result = await _closureService.SubmitProviderOutcomeAsync(token, outcome, notes, DateTime.UtcNow, cancellationToken);
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return View("ProviderCompletion", BuildCompletionModel(result.Context, token, JourneyServiceClosureAudiences.Provider) with
        {
            ActionCompleted = true,
            ActionSucceeded = result.Success,
            FeedbackMessage = result.Message
        });
    }

    [HttpGet("encerramento/cliente")]
    public IActionResult ClientCompletion([FromQuery] string token, [FromQuery(Name = "acao")] string action)
    {
        var context = _closureService.GetClientCompletionContext(token, action, DateTime.UtcNow);
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return View("ClientCompletion", BuildCompletionModel(context, token, JourneyServiceClosureAudiences.Client) with
        {
            Action = JourneyServiceClosureReviewActions.Normalize(action)
        });
    }

    [HttpPost("encerramento/cliente")]
    public async Task<IActionResult> ClientCompletionSubmit([FromForm] string token, [FromForm(Name = "acao")] string action, [FromForm] string reason, CancellationToken cancellationToken)
    {
        var result = await _closureService.SubmitClientDecisionAsync(token, action, reason, DateTime.UtcNow, cancellationToken);
        var nextReviewUrl = string.IsNullOrWhiteSpace(result.NextClientReviewToken)
            ? string.Empty
            : _linkService.BuildReviewUrl(result.NextClientReviewToken).ToString();

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return View("ClientCompletion", BuildCompletionModel(result.Context, token, JourneyServiceClosureAudiences.Client) with
        {
            Action = JourneyServiceClosureReviewActions.Normalize(action),
            ActionCompleted = true,
            ActionSucceeded = result.Success,
            FeedbackMessage = result.Message,
            NextReviewUrl = nextReviewUrl
        });
    }

    [HttpGet("avaliacoes/responder")]
    public IActionResult Review([FromQuery] string token)
    {
        var providerContext = _closureService.GetReviewContext(token, JourneyServiceClosureAudiences.Provider, DateTime.UtcNow);
        var context = providerContext.Success || providerContext.TokenExpired || providerContext.AlreadyResponded || providerContext.NotFound
            ? providerContext
            : _closureService.GetReviewContext(token, JourneyServiceClosureAudiences.Client, DateTime.UtcNow);

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return View("Review", BuildReviewModel(context, token));
    }

    [HttpPost("avaliacoes/responder")]
    public async Task<IActionResult> ReviewSubmit(
        [FromForm] string token,
        [FromForm] string audience,
        [FromForm] int rating,
        [FromForm] string comment,
        [FromForm] string lowScoreReason,
        [FromForm] bool? wouldHireAgain,
        CancellationToken cancellationToken)
    {
        var result = await _closureService.SubmitReviewAsync(
            token,
            audience,
            new JourneyServiceClosureReviewSubmissionRequest
            {
                Rating = rating,
                Comment = comment,
                LowScoreReason = lowScoreReason,
                WouldHireAgain = wouldHireAgain
            },
            DateTime.UtcNow,
            cancellationToken);

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return View("Review", BuildReviewModel(result.Context, token) with
        {
            ActionCompleted = true,
            ActionSucceeded = result.Success,
            FeedbackMessage = result.Message
        });
    }

    private static JourneyServiceClosurePageViewModel BuildCompletionModel(JourneyServiceClosureCompletionContext context, string token, string audience)
    {
        return new JourneyServiceClosurePageViewModel
        {
            Title = "Encerramento da jornada",
            Token = token,
            Audience = audience,
            LeadName = context.LeadName,
            ProviderName = context.ProviderName,
            RequestedCategory = context.RequestedCategory,
            AddressSummary = context.AddressSummary,
            ScheduledWindowLabel = context.ScheduledWindowLabel,
            CompletionStatusLabel = context.CompletionStatusLabel,
            ResponseHeadline = context.ResponseHeadline,
            ResponseDescription = context.ResponseDescription,
            TokenExpired = context.TokenExpired,
            NotFound = context.NotFound,
            CanRespond = !context.TokenExpired && !context.NotFound
        };
    }

    private static JourneyServiceClosurePageViewModel BuildReviewModel(JourneyServiceClosureReviewContext context, string token)
    {
        return new JourneyServiceClosurePageViewModel
        {
            Title = "Avaliacao da jornada",
            Token = token,
            Audience = context.Audience,
            LeadName = context.LeadName,
            CounterpartyName = context.CounterpartyName,
            RequestedCategory = context.RequestedCategory,
            AddressSummary = context.AddressSummary,
            ScheduledWindowLabel = context.ScheduledWindowLabel,
            ResponseHeadline = context.ResponseHeadline,
            ResponseDescription = context.ResponseDescription,
            TokenExpired = context.TokenExpired,
            NotFound = context.NotFound,
            CanRespond = context.CanRespond,
            AlreadyResponded = context.AlreadyResponded
        };
    }
}
