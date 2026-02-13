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
