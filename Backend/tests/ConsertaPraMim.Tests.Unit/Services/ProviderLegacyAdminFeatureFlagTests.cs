using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Web.Provider.Controllers;
using ConsertaPraMim.Web.Provider.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ProviderLegacyAdminFeatureFlagTests
{
    [Fact]
    public async Task Index_ShouldReturnNotFound_WhenLegacyAdminIsDisabled()
    {
        var userRepositoryMock = new Mock<IUserRepository>(MockBehavior.Strict);
        var requestRepositoryMock = new Mock<IServiceRequestRepository>(MockBehavior.Strict);
        var controller = CreateController(userRepositoryMock, requestRepositoryMock, enabled: false);

        var result = await controller.Index();

        Assert.IsType<NotFoundResult>(result);
        userRepositoryMock.Verify(r => r.GetAllAsync(), Times.Never);
        requestRepositoryMock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Users_ShouldReturnView_WhenLegacyAdminIsEnabled()
    {
        var userRepositoryMock = new Mock<IUserRepository>();
        var requestRepositoryMock = new Mock<IServiceRequestRepository>(MockBehavior.Strict);
        userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var controller = CreateController(userRepositoryMock, requestRepositoryMock, enabled: true);

        var result = await controller.Users();

        Assert.IsType<ViewResult>(result);
        userRepositoryMock.Verify(r => r.GetAllAsync(), Times.Once);
    }

    private static AdminController CreateController(
        Mock<IUserRepository> userRepositoryMock,
        Mock<IServiceRequestRepository> requestRepositoryMock,
        bool enabled)
    {
        return new AdminController(
            userRepositoryMock.Object,
            requestRepositoryMock.Object,
            Options.Create(new LegacyAdminOptions { Enabled = enabled }),
            NullLogger<AdminController>.Instance);
    }
}
