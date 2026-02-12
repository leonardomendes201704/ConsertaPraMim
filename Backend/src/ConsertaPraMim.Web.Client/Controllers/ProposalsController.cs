using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ConsertaPraMim.Application.Interfaces;
using System.Security.Claims;

namespace ConsertaPraMim.Web.Client.Controllers;

[Authorize(Roles = "Client")]
public class ProposalsController : Controller
{
    private readonly IProposalService _proposalService;

    public ProposalsController(IProposalService proposalService)
    {
        _proposalService = proposalService;
    }

    [HttpPost]
    public async Task<IActionResult> Accept(Guid proposalId)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);

        var success = await _proposalService.AcceptAsync(proposalId, userId);

        if (success)
        {
            TempData["Success"] = "Proposta aceita com sucesso! O profissional foi notificado.";
        }
        else
        {
            TempData["Error"] = "Não foi possível aceitar a proposta. Tente novamente.";
        }

        return Redirect(Request.Headers["Referer"].ToString());
    }
}
