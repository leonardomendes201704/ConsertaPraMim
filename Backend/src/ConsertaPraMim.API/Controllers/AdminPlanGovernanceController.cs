using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/plan-governance")]
public class AdminPlanGovernanceController : ControllerBase
{
    private readonly IPlanGovernanceService _planGovernanceService;

    public AdminPlanGovernanceController(IPlanGovernanceService planGovernanceService)
    {
        _planGovernanceService = planGovernanceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSnapshot(
        [FromQuery] bool includeInactivePromotions = true,
        [FromQuery] bool includeInactiveCoupons = true)
    {
        var snapshot = await _planGovernanceService.GetAdminSnapshotAsync(includeInactivePromotions, includeInactiveCoupons);
        return Ok(snapshot);
    }

    /// <summary>
    /// Consolida receita por componente do modelo hibrido (assinatura fixa x creditos por resultado).
    /// </summary>
    /// <remarks>
    /// Retorna:
    /// - MRR fixo estimado por plano ativo;
    /// - receita variavel realizada via debitos de creditos no periodo;
    /// - participacao percentual de cada componente;
    /// - serie diaria para leitura de tendencia operacional.
    /// </remarks>
    /// <param name="fromUtc">Inicio opcional do recorte em UTC (padrao: 30 dias atras).</param>
    /// <param name="toUtc">Fim opcional do recorte em UTC (padrao: agora).</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Painel consolidado de receita por componente.</returns>
    [HttpGet("revenue-components")]
    [ProducesResponseType(typeof(AdminRevenueComponentDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRevenueComponents(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dashboard = await _planGovernanceService.GetRevenueComponentDashboardAsync(fromUtc, toUtc, cancellationToken);
            return Ok(dashboard);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errorCode = "validation_error", errorMessage = ex.Message });
        }
    }

    /// <summary>
    /// Retorna estrategia de rollout por cohort para o modelo hibrido de monetizacao.
    /// </summary>
    /// <remarks>
    /// O painel consolida elegibilidade operacional por trust/compliance/plano e define fases de liberacao
    /// com guardrails para expansao progressiva do modelo assinatura + creditos.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Estrategia de rollout por cohorts com fases e criterio de governanca.</returns>
    [HttpGet("hybrid-rollout")]
    [ProducesResponseType(typeof(AdminHybridRolloutStrategyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetHybridRollout(CancellationToken cancellationToken = default)
    {
        var strategy = await _planGovernanceService.GetHybridRolloutStrategyAsync(cancellationToken);
        return Ok(strategy);
    }

    [HttpPut("settings/{plan}")]
    public async Task<IActionResult> UpdatePlanSetting(
        ProviderPlan plan,
        [FromBody] AdminUpdatePlanSettingRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _planGovernanceService.UpdatePlanSettingAsync(plan, request, actorUserId, actorEmail);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "invalid_plan" => BadRequest(result),
            "validation_error" => BadRequest(result),
            _ => BadRequest(result)
        };
    }

    [HttpPost("promotions")]
    public async Task<IActionResult> CreatePromotion([FromBody] AdminCreatePlanPromotionRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _planGovernanceService.CreatePromotionAsync(request, actorUserId, actorEmail);
        if (result.Success)
        {
            return CreatedAtAction(nameof(GetSnapshot), result);
        }

        return BadRequest(result);
    }

    [HttpPut("promotions/{promotionId:guid}")]
    public async Task<IActionResult> UpdatePromotion(Guid promotionId, [FromBody] AdminUpdatePlanPromotionRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _planGovernanceService.UpdatePromotionAsync(promotionId, request, actorUserId, actorEmail);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "not_found" => NotFound(result),
            _ => BadRequest(result)
        };
    }

    [HttpPut("promotions/{promotionId:guid}/status")]
    public async Task<IActionResult> UpdatePromotionStatus(Guid promotionId, [FromBody] AdminUpdatePlanPromotionStatusRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _planGovernanceService.UpdatePromotionStatusAsync(promotionId, request, actorUserId, actorEmail);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "not_found" => NotFound(result),
            _ => BadRequest(result)
        };
    }

    [HttpPost("coupons")]
    public async Task<IActionResult> CreateCoupon([FromBody] AdminCreatePlanCouponRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _planGovernanceService.CreateCouponAsync(request, actorUserId, actorEmail);
        if (result.Success)
        {
            return CreatedAtAction(nameof(GetSnapshot), result);
        }

        return result.ErrorCode switch
        {
            "duplicate_code" => Conflict(result),
            _ => BadRequest(result)
        };
    }

    [HttpPut("coupons/{couponId:guid}")]
    public async Task<IActionResult> UpdateCoupon(Guid couponId, [FromBody] AdminUpdatePlanCouponRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _planGovernanceService.UpdateCouponAsync(couponId, request, actorUserId, actorEmail);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "not_found" => NotFound(result),
            _ => BadRequest(result)
        };
    }

    [HttpPut("coupons/{couponId:guid}/status")]
    public async Task<IActionResult> UpdateCouponStatus(Guid couponId, [FromBody] AdminUpdatePlanCouponStatusRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _planGovernanceService.UpdateCouponStatusAsync(couponId, request, actorUserId, actorEmail);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "not_found" => NotFound(result),
            _ => BadRequest(result)
        };
    }

    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate([FromBody] AdminPlanPriceSimulationRequestDto request)
    {
        var result = await _planGovernanceService.SimulatePriceAsync(request);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "coupon_not_found" => NotFound(result),
            "coupon_global_limit" => Conflict(result),
            "coupon_provider_limit" => Conflict(result),
            _ => BadRequest(result)
        };
    }

    private bool TryGetActor(out Guid actorUserId, out string actorEmail)
    {
        actorEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        actorUserId = default;

        var actorRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(actorRaw) && Guid.TryParse(actorRaw, out actorUserId);
    }
}
