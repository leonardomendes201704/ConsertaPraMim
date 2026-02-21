using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Provider.Controllers;
using ConsertaPraMim.Web.Provider.Options;
using ConsertaPraMim.Web.Provider.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ProviderLegacyAdminFeatureFlagTests
{
    [Fact(DisplayName = "Prestador legacy admin feature flag | Index | Deve retornar nao encontrado quando legacy admin disabled")]
    public async Task Index_ShouldReturnNotFound_WhenLegacyAdminIsDisabled()
    {
        var legacyAdminApiClientMock = new Mock<IProviderLegacyAdminApiClient>(MockBehavior.Strict);
        var controller = CreateController(legacyAdminApiClientMock, enabled: false);

        var result = await controller.Index();

        Assert.IsType<NotFoundResult>(result);
        legacyAdminApiClientMock.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "Prestador legacy admin feature flag | Usuarios | Deve retornar view quando legacy admin enabled")]
    public async Task Users_ShouldReturnView_WhenLegacyAdminIsEnabled()
    {
        var legacyAdminApiClientMock = new Mock<IProviderLegacyAdminApiClient>();
        legacyAdminApiClientMock
            .Setup(client => client.GetUsersAsync(
                null,
                null,
                null,
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<AdminUserListItemDto>(), 0, null as string));
        var controller = CreateController(legacyAdminApiClientMock, enabled: true);

        var result = await controller.Users();

        Assert.IsType<ViewResult>(result);
        legacyAdminApiClientMock.Verify(
            client => client.GetUsersAsync(
                null,
                null,
                null,
                1,
                200,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static AdminController CreateController(
        Mock<IProviderLegacyAdminApiClient> legacyAdminApiClientMock,
        bool enabled)
    {
        var controller = new AdminController(
            legacyAdminApiClientMock.Object,
            Options.Create(new LegacyAdminOptions { Enabled = enabled }),
            NullLogger<AdminController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }
}
