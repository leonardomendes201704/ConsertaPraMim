using System.Security.Claims;
using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class ServiceAppointmentsControllerTests
{
    [Fact]
    public async Task GetSlots_ShouldReturnUnauthorized_WhenNameIdentifierIsMissing()
    {
        var serviceMock = new Mock<IServiceAppointmentService>();
        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetSlots(new GetServiceAppointmentSlotsQueryDto(
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(4)));

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Create_ShouldReturnConflict_WhenSlotIsUnavailable()
    {
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CreateServiceAppointmentRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "slot_unavailable",
                ErrorMessage: "Janela indisponivel."));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Client.ToString());

        var result = await controller.Create(new CreateServiceAppointmentRequestDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(3)));

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task GetMine_ShouldReturnOkWithAppointments()
    {
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.GetMyAppointmentsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<ServiceAppointmentDto>
            {
                new(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    ServiceAppointmentStatus.PendingProviderConfirmation.ToString(),
                    DateTime.UtcNow.AddHours(4),
                    DateTime.UtcNow.AddHours(5),
                    DateTime.UtcNow.AddHours(1),
                    null,
                    DateTime.UtcNow,
                    null,
                    Array.Empty<ServiceAppointmentHistoryDto>())
            });

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());

        var result = await controller.GetMine(null, null);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ServiceAppointmentDto>>(ok.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task GetById_ShouldReturnOk_WhenAppointmentExists()
    {
        var appointment = new ServiceAppointmentDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ServiceAppointmentStatus.PendingProviderConfirmation.ToString(),
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(3),
            DateTime.UtcNow.AddHours(1),
            "Teste",
            DateTime.UtcNow,
            null,
            Array.Empty<ServiceAppointmentHistoryDto>());

        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<string>(), appointment.Id))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(true, appointment));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Client.ToString());

        var result = await controller.GetById(appointment.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ServiceAppointmentDto>(ok.Value);
        Assert.Equal(appointment.Id, dto.Id);
    }

    [Fact]
    public async Task Confirm_ShouldReturnConflict_WhenServiceReturnsInvalidState()
    {
        var appointmentId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.ConfirmAsync(It.IsAny<Guid>(), It.IsAny<string>(), appointmentId))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "invalid_state",
                ErrorMessage: "Status invalido."));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());

        var result = await controller.Confirm(appointmentId);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Reject_ShouldReturnOk_WhenServiceRejectsSuccessfully()
    {
        var appointment = new ServiceAppointmentDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ServiceAppointmentStatus.RejectedByProvider.ToString(),
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(3),
            null,
            "Nao tenho disponibilidade",
            DateTime.UtcNow,
            null,
            Array.Empty<ServiceAppointmentHistoryDto>());

        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.RejectAsync(It.IsAny<Guid>(), It.IsAny<string>(), appointment.Id, It.IsAny<RejectServiceAppointmentRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(true, appointment));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());

        var result = await controller.Reject(appointment.Id, new RejectServiceAppointmentRequestDto("Nao tenho disponibilidade"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ServiceAppointmentDto>(ok.Value);
        Assert.Equal(ServiceAppointmentStatus.RejectedByProvider.ToString(), dto.Status);
    }

    private static ServiceAppointmentsController CreateController(
        IServiceAppointmentService service,
        Guid? userId = null,
        string? role = null)
    {
        var controller = new ServiceAppointmentsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        if (userId.HasValue)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.Value.ToString())
            };

            if (!string.IsNullOrWhiteSpace(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(claims, "TestAuth"));
        }

        return controller;
    }
}
