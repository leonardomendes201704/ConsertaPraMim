using ConsertaPraMim.Web.Client.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Client.Controllers;

[Authorize(Roles = "Client")]
public class ProposalsController : Controller
{
    private readonly IClientProposalApiClient _proposalApiClient;

    public ProposalsController(IClientProposalApiClient proposalApiClient)
    {
        _proposalApiClient = proposalApiClient;
    }

    [HttpPost]
    public async Task<IActionResult> Accept(Guid proposalId)
    {
        var (success, errorMessage) = await _proposalApiClient.AcceptAsync(proposalId, HttpContext.RequestAborted);

        if (success)
        {
            TempData["Success"] = "Proposta aceita com sucesso! O profissional foi notificado.";
        }
        else
        {
            TempData["Error"] = errorMessage ?? "Nao foi possivel aceitar a proposta. Tente novamente.";
        }

        return Redirect(Request.Headers["Referer"].ToString());
    }
}
