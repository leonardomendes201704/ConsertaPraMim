using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminPlanGovernanceController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminPlanGovernanceController(IAdminOperationsApiClient adminOperationsApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        bool includeInactivePromotions = true,
        bool includeInactiveCoupons = true)
    {
        var model = new AdminPlanGovernanceIndexViewModel
        {
            IncludeInactivePromotions = includeInactivePromotions,
            IncludeInactiveCoupons = includeInactiveCoupons
        };

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var result = await _adminOperationsApiClient.GetPlanGovernanceSnapshotAsync(
            includeInactivePromotions,
            includeInactiveCoupons,
            token,
            HttpContext.RequestAborted);

        if (!result.Success || result.Data == null)
        {
            model.ErrorMessage = result.ErrorMessage ?? "Falha ao carregar governanca de planos.";
            return View(model);
        }

        model.Snapshot = result.Data;
        model.LastUpdatedUtc = DateTime.UtcNow;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePlanSetting([FromBody] AdminUpdatePlanSettingWebRequest request)
    {
        if (request == null || !TryParsePlan(request.Plan, out var plan))
        {
            return BadRequest(new { success = false, errorMessage = "Plano invalido." });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Token administrativo ausente. Faca login novamente." });
        }

        var apiRequest = new AdminUpdatePlanSettingRequestDto(
            request.MonthlyPrice,
            request.MaxRadiusKm,
            request.MaxAllowedCategories,
            request.AllowedCategories ?? new List<string>());

        var result = await _adminOperationsApiClient.UpdatePlanSettingAsync(plan.ToString(), apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode ?? StatusCodes.Status400BadRequest, new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Nao foi possivel atualizar o plano.",
                errorCode = result.ErrorCode
            });
        }

        return Ok(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> CreatePromotion([FromBody] AdminCreatePlanPromotionWebRequest request)
    {
        if (request == null || !TryParsePlan(request.Plan, out var plan) || !TryParseDiscountType(request.DiscountType, out var discountType))
        {
            return BadRequest(new { success = false, errorMessage = "Dados da promocao invalidos." });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Token administrativo ausente. Faca login novamente." });
        }

        var apiRequest = new AdminCreatePlanPromotionRequestDto(
            plan,
            request.Name,
            discountType,
            request.DiscountValue,
            request.StartsAtUtc,
            request.EndsAtUtc);

        var result = await _adminOperationsApiClient.CreatePlanPromotionAsync(apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode ?? StatusCodes.Status400BadRequest, new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Nao foi possivel criar a promocao.",
                errorCode = result.ErrorCode
            });
        }

        return Ok(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePromotion([FromBody] AdminUpdatePlanPromotionWebRequest request)
    {
        if (request == null || request.PromotionId == Guid.Empty || !TryParseDiscountType(request.DiscountType, out var discountType))
        {
            return BadRequest(new { success = false, errorMessage = "Dados da promocao invalidos." });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Token administrativo ausente. Faca login novamente." });
        }

        var apiRequest = new AdminUpdatePlanPromotionRequestDto(
            request.Name,
            discountType,
            request.DiscountValue,
            request.StartsAtUtc,
            request.EndsAtUtc);

        var result = await _adminOperationsApiClient.UpdatePlanPromotionAsync(request.PromotionId, apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode ?? StatusCodes.Status400BadRequest, new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Nao foi possivel atualizar a promocao.",
                errorCode = result.ErrorCode
            });
        }

        return Ok(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePromotionStatus([FromBody] AdminUpdatePlanPromotionStatusWebRequest request)
    {
        if (request == null || request.PromotionId == Guid.Empty)
        {
            return BadRequest(new { success = false, errorMessage = "Promocao invalida." });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Token administrativo ausente. Faca login novamente." });
        }

        var apiRequest = new AdminUpdatePlanPromotionStatusRequestDto(request.IsActive, request.Reason);
        var result = await _adminOperationsApiClient.UpdatePlanPromotionStatusAsync(request.PromotionId, apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode ?? StatusCodes.Status400BadRequest, new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Nao foi possivel alterar o status da promocao.",
                errorCode = result.ErrorCode
            });
        }

        return Ok(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> CreateCoupon([FromBody] AdminCreatePlanCouponWebRequest request)
    {
        if (request == null ||
            !TryParseDiscountType(request.DiscountType, out var discountType) ||
            !TryParseNullablePlan(request.Plan, out var plan))
        {
            return BadRequest(new { success = false, errorMessage = "Dados do cupom invalidos." });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Token administrativo ausente. Faca login novamente." });
        }

        var apiRequest = new AdminCreatePlanCouponRequestDto(
            request.Code,
            request.Name,
            plan,
            discountType,
            request.DiscountValue,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.MaxGlobalUses,
            request.MaxUsesPerProvider);

        var result = await _adminOperationsApiClient.CreatePlanCouponAsync(apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode ?? StatusCodes.Status400BadRequest, new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Nao foi possivel criar o cupom.",
                errorCode = result.ErrorCode
            });
        }

        return Ok(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateCoupon([FromBody] AdminUpdatePlanCouponWebRequest request)
    {
        if (request == null ||
            request.CouponId == Guid.Empty ||
            !TryParseDiscountType(request.DiscountType, out var discountType) ||
            !TryParseNullablePlan(request.Plan, out var plan))
        {
            return BadRequest(new { success = false, errorMessage = "Dados do cupom invalidos." });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Token administrativo ausente. Faca login novamente." });
        }

        var apiRequest = new AdminUpdatePlanCouponRequestDto(
            request.Name,
            plan,
            discountType,
            request.DiscountValue,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.MaxGlobalUses,
            request.MaxUsesPerProvider);

        var result = await _adminOperationsApiClient.UpdatePlanCouponAsync(request.CouponId, apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode ?? StatusCodes.Status400BadRequest, new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Nao foi possivel atualizar o cupom.",
                errorCode = result.ErrorCode
            });
        }

        return Ok(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateCouponStatus([FromBody] AdminUpdatePlanCouponStatusWebRequest request)
    {
        if (request == null || request.CouponId == Guid.Empty)
        {
            return BadRequest(new { success = false, errorMessage = "Cupom invalido." });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Token administrativo ausente. Faca login novamente." });
        }

        var apiRequest = new AdminUpdatePlanCouponStatusRequestDto(request.IsActive, request.Reason);
        var result = await _adminOperationsApiClient.UpdatePlanCouponStatusAsync(request.CouponId, apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode ?? StatusCodes.Status400BadRequest, new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Nao foi possivel alterar o status do cupom.",
                errorCode = result.ErrorCode
            });
        }

        return Ok(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> Simulate([FromBody] AdminPlanSimulationWebRequest request)
    {
        if (request == null || !TryParsePlan(request.Plan, out var plan))
        {
            return BadRequest(new { success = false, errorMessage = "Plano invalido para simulacao." });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Token administrativo ausente. Faca login novamente." });
        }

        var apiRequest = new AdminPlanPriceSimulationRequestDto(plan, request.CouponCode, request.AtUtc, request.ProviderUserId);
        var result = await _adminOperationsApiClient.SimulatePlanPriceAsync(apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null)
        {
            return StatusCode(result.StatusCode ?? StatusCodes.Status400BadRequest, new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Nao foi possivel simular o preco.",
                errorCode = result.ErrorCode
            });
        }

        return Ok(new
        {
            success = true,
            simulation = result.Data
        });
    }

    private string? GetAccessToken()
    {
        return User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
    }

    private static bool TryParsePlan(string value, out ProviderPlan plan)
    {
        if (!Enum.TryParse(value, true, out plan))
        {
            return false;
        }

        return plan is ProviderPlan.Bronze or ProviderPlan.Silver or ProviderPlan.Gold;
    }

    private static bool TryParseNullablePlan(string? value, out ProviderPlan? plan)
    {
        plan = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!TryParsePlan(value, out var parsed))
        {
            return false;
        }

        plan = parsed;
        return true;
    }

    private static bool TryParseDiscountType(string value, out PricingDiscountType discountType)
    {
        return Enum.TryParse(value, true, out discountType) &&
               (discountType == PricingDiscountType.Percentage || discountType == PricingDiscountType.FixedAmount);
    }
}
