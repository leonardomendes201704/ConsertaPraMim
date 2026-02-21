using System.Security.Claims;
using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Data;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Tests.Unit.Integration.Controllers;

public class AdminPlanGovernanceControllerSqliteIntegrationTests
{
    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin plan governance controller sqlite integracao | Atualizar plan setting | Deve persistir setting e write audit.
    /// </summary>
    [Fact(DisplayName = "Admin plan governance controller sqlite integracao | Atualizar plan setting | Deve persistir setting e write audit")]
    public async Task UpdatePlanSetting_ShouldPersistSetting_AndWriteAudit()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var actorUserId = Guid.NewGuid();
            var controller = BuildController(context, actorUserId, "admin.plan@teste.com");

            var response = await controller.UpdatePlanSetting(
                ProviderPlan.Bronze,
                new AdminUpdatePlanSettingRequestDto(
                    MonthlyPrice: 89.90m,
                    MaxRadiusKm: 35,
                    MaxAllowedCategories: 3,
                    AllowedCategories: new List<string> { "Eletrica", "Hidraulica", "Limpeza" }));

            var ok = Assert.IsType<OkObjectResult>(response);
            var payload = Assert.IsType<AdminOperationResultDto>(ok.Value);
            Assert.True(payload.Success);

            var setting = await context.ProviderPlanSettings.AsNoTracking().SingleAsync(x => x.Plan == ProviderPlan.Bronze);
            Assert.Equal(89.90m, setting.MonthlyPrice);
            Assert.Equal(35, setting.MaxRadiusKm);
            Assert.Equal(3, setting.MaxAllowedCategories);
            Assert.Contains(ServiceCategory.Electrical, setting.AllowedCategories);
            Assert.Contains(ServiceCategory.Plumbing, setting.AllowedCategories);
            Assert.Contains(ServiceCategory.Cleaning, setting.AllowedCategories);

            var audit = await context.AdminAuditLogs
                .AsNoTracking()
                .SingleAsync(x => x.Action == "ProviderPlanSettingUpdated" && x.TargetId == setting.Id);
            Assert.Equal("ProviderPlanGovernance", audit.TargetType);
            Assert.Equal(actorUserId, audit.ActorUserId);
        }
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin plan governance controller sqlite integracao | Promotion endpoints | Deve criar atualizar e toggle status.
    /// </summary>
    [Fact(DisplayName = "Admin plan governance controller sqlite integracao | Promotion endpoints | Deve criar atualizar e toggle status")]
    public async Task PromotionEndpoints_ShouldCreateUpdateAndToggleStatus()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var actorUserId = Guid.NewGuid();
            var controller = BuildController(context, actorUserId, "admin.promocao@teste.com");
            var now = DateTime.UtcNow;

            var createResponse = await controller.CreatePromotion(
                new AdminCreatePlanPromotionRequestDto(
                    Plan: ProviderPlan.Silver,
                    Name: "Campanha Semana do Cliente",
                    DiscountType: PricingDiscountType.Percentage,
                    DiscountValue: 10m,
                    StartsAtUtc: now.AddDays(-1),
                    EndsAtUtc: now.AddDays(10)));

            Assert.IsType<CreatedAtActionResult>(createResponse);

            var promotion = await context.ProviderPlanPromotions
                .AsNoTracking()
                .SingleAsync(x => x.Plan == ProviderPlan.Silver && x.Name == "Campanha Semana do Cliente");

            var updateResponse = await controller.UpdatePromotion(
                promotion.Id,
                new AdminUpdatePlanPromotionRequestDto(
                    Name: "Campanha Atualizada",
                    DiscountType: PricingDiscountType.FixedAmount,
                    DiscountValue: 20m,
                    StartsAtUtc: now.AddDays(-2),
                    EndsAtUtc: now.AddDays(15)));

            var updateOk = Assert.IsType<OkObjectResult>(updateResponse);
            Assert.True(Assert.IsType<AdminOperationResultDto>(updateOk.Value).Success);

            var statusResponse = await controller.UpdatePromotionStatus(
                promotion.Id,
                new AdminUpdatePlanPromotionStatusRequestDto(
                    IsActive: false,
                    Reason: "Pausada para revisao"));

            var statusOk = Assert.IsType<OkObjectResult>(statusResponse);
            Assert.True(Assert.IsType<AdminOperationResultDto>(statusOk.Value).Success);

            var updated = await context.ProviderPlanPromotions.AsNoTracking().SingleAsync(x => x.Id == promotion.Id);
            Assert.Equal("Campanha Atualizada", updated.Name);
            Assert.Equal(PricingDiscountType.FixedAmount, updated.DiscountType);
            Assert.Equal(20m, updated.DiscountValue);
            Assert.False(updated.IsActive);

            var auditActions = await context.AdminAuditLogs
                .AsNoTracking()
                .Where(x => x.TargetType == "ProviderPlanGovernance" && x.TargetId == promotion.Id)
                .Select(x => x.Action)
                .ToListAsync();

            Assert.Contains("ProviderPlanPromotionCreated", auditActions);
            Assert.Contains("ProviderPlanPromotionUpdated", auditActions);
            Assert.Contains("ProviderPlanPromotionStatusChanged", auditActions);
        }
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin plan governance controller sqlite integracao | Coupon endpoints | Deve criar atualizar toggle status e block duplicate code.
    /// </summary>
    [Fact(DisplayName = "Admin plan governance controller sqlite integracao | Coupon endpoints | Deve criar atualizar toggle status e block duplicate code")]
    public async Task CouponEndpoints_ShouldCreateUpdateToggleStatus_AndBlockDuplicateCode()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var actorUserId = Guid.NewGuid();
            var controller = BuildController(context, actorUserId, "admin.cupom@teste.com");
            var now = DateTime.UtcNow;

            var createResponse = await controller.CreateCoupon(
                new AdminCreatePlanCouponRequestDto(
                    Code: "PROMO10",
                    Name: "Cupom inaugural",
                    Plan: ProviderPlan.Bronze,
                    DiscountType: PricingDiscountType.Percentage,
                    DiscountValue: 10m,
                    StartsAtUtc: now.AddDays(-1),
                    EndsAtUtc: now.AddDays(30),
                    MaxGlobalUses: 100,
                    MaxUsesPerProvider: 1));

            Assert.IsType<CreatedAtActionResult>(createResponse);

            var duplicateResponse = await controller.CreateCoupon(
                new AdminCreatePlanCouponRequestDto(
                    Code: " promo10 ",
                    Name: "Cupom duplicado",
                    Plan: ProviderPlan.Bronze,
                    DiscountType: PricingDiscountType.Percentage,
                    DiscountValue: 5m,
                    StartsAtUtc: now.AddDays(-1),
                    EndsAtUtc: now.AddDays(30),
                    MaxGlobalUses: 50,
                    MaxUsesPerProvider: 1));

            Assert.IsType<ConflictObjectResult>(duplicateResponse);

            var coupon = await context.ProviderPlanCoupons.AsNoTracking().SingleAsync(x => x.Code == "PROMO10");

            var updateResponse = await controller.UpdateCoupon(
                coupon.Id,
                new AdminUpdatePlanCouponRequestDto(
                    Name: "Cupom atualizado",
                    Plan: ProviderPlan.Silver,
                    DiscountType: PricingDiscountType.FixedAmount,
                    DiscountValue: 15m,
                    StartsAtUtc: now.AddDays(-2),
                    EndsAtUtc: now.AddDays(40),
                    MaxGlobalUses: 200,
                    MaxUsesPerProvider: 2));

            var updateOk = Assert.IsType<OkObjectResult>(updateResponse);
            Assert.True(Assert.IsType<AdminOperationResultDto>(updateOk.Value).Success);

            var statusResponse = await controller.UpdateCouponStatus(
                coupon.Id,
                new AdminUpdatePlanCouponStatusRequestDto(
                    IsActive: false,
                    Reason: "Encerrado"));

            var statusOk = Assert.IsType<OkObjectResult>(statusResponse);
            Assert.True(Assert.IsType<AdminOperationResultDto>(statusOk.Value).Success);

            var updated = await context.ProviderPlanCoupons.AsNoTracking().SingleAsync(x => x.Id == coupon.Id);
            Assert.Equal("Cupom atualizado", updated.Name);
            Assert.Equal(ProviderPlan.Silver, updated.Plan);
            Assert.Equal(PricingDiscountType.FixedAmount, updated.DiscountType);
            Assert.Equal(15m, updated.DiscountValue);
            Assert.Equal(200, updated.MaxGlobalUses);
            Assert.Equal(2, updated.MaxUsesPerProvider);
            Assert.False(updated.IsActive);

            var auditActions = await context.AdminAuditLogs
                .AsNoTracking()
                .Where(x => x.TargetType == "ProviderPlanGovernance" && x.TargetId == coupon.Id)
                .Select(x => x.Action)
                .ToListAsync();

            Assert.Contains("ProviderPlanCouponCreated", auditActions);
            Assert.Contains("ProviderPlanCouponUpdated", auditActions);
            Assert.Contains("ProviderPlanCouponStatusChanged", auditActions);
        }
    }

    private static AdminPlanGovernanceController BuildController(
        ConsertaPraMimDbContext context,
        Guid actorUserId,
        string actorEmail)
    {
        var service = new PlanGovernanceService(
            new ProviderPlanGovernanceRepository(context),
            new ProviderCreditRepository(context),
            new AdminAuditLogRepository(context),
            new UserRepository(context));

        return new AdminPlanGovernanceController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, actorUserId.ToString()),
                        new Claim(ClaimTypes.Email, actorEmail),
                        new Claim(ClaimTypes.Role, UserRole.Admin.ToString())
                    }))
                }
            }
        };
    }
}
