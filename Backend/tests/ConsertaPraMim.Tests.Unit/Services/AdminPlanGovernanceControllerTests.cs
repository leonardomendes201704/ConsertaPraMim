using System.Security.Claims;
using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminPlanGovernanceControllerTests
{
    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin plan governance controller | Controller | Deve protected com admin only politica.
    /// </summary>
    [Fact(DisplayName = "Admin plan governance controller | Controller | Deve protected com admin only politica")]
    public void Controller_ShouldBeProtectedWithAdminOnlyPolicy()
    {
        var authorize = typeof(AdminPlanGovernanceController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("AdminOnly", authorize!.Policy);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin plan governance controller | Atualizar plan setting | Deve retornar nao autorizado quando actor claim missing.
    /// </summary>
    [Fact(DisplayName = "Admin plan governance controller | Atualizar plan setting | Deve retornar nao autorizado quando actor claim missing")]
    public async Task UpdatePlanSetting_ShouldReturnUnauthorized_WhenActorClaimIsMissing()
    {
        var serviceMock = new Mock<IPlanGovernanceService>();
        var controller = new AdminPlanGovernanceController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.UpdatePlanSetting(
            ProviderPlan.Bronze,
            new AdminUpdatePlanSettingRequestDto(79.90m, 25, 3, new List<string> { "Eletrica" }));

        Assert.IsType<UnauthorizedResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin plan governance controller | Criar coupon | Deve retornar conflito quando servico returns duplicate code.
    /// </summary>
    [Fact(DisplayName = "Admin plan governance controller | Criar coupon | Deve retornar conflito quando servico returns duplicate code")]
    public async Task CreateCoupon_ShouldReturnConflict_WhenServiceReturnsDuplicateCode()
    {
        var actorUserId = Guid.NewGuid();
        var serviceMock = new Mock<IPlanGovernanceService>();
        serviceMock
            .Setup(x => x.CreateCouponAsync(
                It.IsAny<AdminCreatePlanCouponRequestDto>(),
                actorUserId,
                "admin@teste.com"))
            .ReturnsAsync(new AdminOperationResultDto(false, "duplicate_code", "codigo repetido"));

        var controller = new AdminPlanGovernanceController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = BuildHttpContext(actorUserId)
            }
        };

        var result = await controller.CreateCoupon(
            new AdminCreatePlanCouponRequestDto(
                "PROMO10",
                "Promocao",
                ProviderPlan.Bronze,
                PricingDiscountType.Percentage,
                10m,
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow.AddDays(1),
                100,
                1));

        Assert.IsType<ConflictObjectResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin plan governance controller | Simulate | Deve retornar nao encontrado quando coupon nao encontrado.
    /// </summary>
    [Fact(DisplayName = "Admin plan governance controller | Simulate | Deve retornar nao encontrado quando coupon nao encontrado")]
    public async Task Simulate_ShouldReturnNotFound_WhenCouponNotFound()
    {
        var serviceMock = new Mock<IPlanGovernanceService>();
        serviceMock
            .Setup(x => x.SimulatePriceAsync(It.IsAny<AdminPlanPriceSimulationRequestDto>()))
            .ReturnsAsync(new AdminPlanPriceSimulationResultDto(
                false,
                100m,
                0m,
                0m,
                100m,
                null,
                null,
                ErrorCode: "coupon_not_found",
                ErrorMessage: "nao encontrado"));

        var controller = new AdminPlanGovernanceController(serviceMock.Object);

        var result = await controller.Simulate(new AdminPlanPriceSimulationRequestDto(
            ProviderPlan.Bronze,
            "CUPOMX",
            DateTime.UtcNow,
            null));

        Assert.IsType<NotFoundObjectResult>(result);
    }

    private static DefaultHttpContext BuildHttpContext(Guid actorUserId)
    {
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, actorUserId.ToString()),
                new Claim(ClaimTypes.Email, "admin@teste.com")
            }))
        };
    }
}
