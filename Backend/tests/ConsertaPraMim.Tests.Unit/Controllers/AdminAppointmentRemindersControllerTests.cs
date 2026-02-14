using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class AdminAppointmentRemindersControllerTests
{
    [Fact]
    public async Task Get_ShouldReturnOkWithPayload()
    {
        var serviceMock = new Mock<IAppointmentReminderService>();
        serviceMock
            .Setup(s => s.GetDispatchesAsync(It.IsAny<AppointmentReminderDispatchQueryDto>()))
            .ReturnsAsync(new AppointmentReminderDispatchListResultDto(
                Array.Empty<AppointmentReminderDispatchDto>(),
                0,
                1,
                50));

        var controller = new AdminAppointmentRemindersController(serviceMock.Object);

        var result = await controller.Get(new AppointmentReminderDispatchQueryDto(Page: 1, PageSize: 50));

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AppointmentReminderDispatchListResultDto>(ok.Value);
        Assert.Empty(payload.Items);
        Assert.Equal(0, payload.Total);
    }
}
