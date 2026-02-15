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
                    null,
                    null,
                    null,
                    null,
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
            null,
            null,
            null,
            null,
            null,
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
            null,
            null,
            null,
            null,
            null,
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

    [Fact]
    public async Task RequestReschedule_ShouldReturnOk_WhenServiceSucceeds()
    {
        var appointment = new ServiceAppointmentDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ServiceAppointmentStatus.RescheduleRequestedByClient.ToString(),
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(1),
            null,
            "Reagendamento solicitado",
            DateTime.UtcNow.AddDays(1).AddHours(2),
            DateTime.UtcNow.AddDays(1).AddHours(3),
            DateTime.UtcNow,
            UserRole.Client.ToString(),
            "Compromisso pessoal",
            DateTime.UtcNow,
            DateTime.UtcNow,
            Array.Empty<ServiceAppointmentHistoryDto>());

        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.RequestRescheduleAsync(It.IsAny<Guid>(), It.IsAny<string>(), appointment.Id, It.IsAny<RequestServiceAppointmentRescheduleDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(true, appointment));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Client.ToString());

        var result = await controller.RequestReschedule(
            appointment.Id,
            new RequestServiceAppointmentRescheduleDto(
                DateTime.UtcNow.AddDays(1).AddHours(2),
                DateTime.UtcNow.AddDays(1).AddHours(3),
                "Compromisso pessoal"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ServiceAppointmentDto>(ok.Value);
        Assert.Equal(ServiceAppointmentStatus.RescheduleRequestedByClient.ToString(), dto.Status);
    }

    [Fact]
    public async Task Cancel_ShouldReturnConflict_WhenPolicyIsViolated()
    {
        var appointmentId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.CancelAsync(It.IsAny<Guid>(), It.IsAny<string>(), appointmentId, It.IsAny<CancelServiceAppointmentRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "policy_violation",
                ErrorMessage: "Antecedencia insuficiente."));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Client.ToString());

        var result = await controller.Cancel(appointmentId, new CancelServiceAppointmentRequestDto("Nao estarei em casa"));

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task MarkArrived_ShouldReturnConflict_WhenServiceReturnsDuplicateCheckin()
    {
        var appointmentId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.MarkArrivedAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                It.IsAny<MarkServiceAppointmentArrivalRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "duplicate_checkin",
                ErrorMessage: "Chegada ja registrada."));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());

        var result = await controller.MarkArrived(
            appointmentId,
            new MarkServiceAppointmentArrivalRequestDto(-24.01, -46.41, 10));

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task StartExecution_ShouldReturnOk_WhenServiceSucceeds()
    {
        var appointment = new ServiceAppointmentDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ServiceAppointmentStatus.InProgress.ToString(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(2),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            Array.Empty<ServiceAppointmentHistoryDto>(),
            DateTime.UtcNow.AddMinutes(-5),
            -24.01,
            -46.41,
            8.0,
            null,
            DateTime.UtcNow);

        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.StartExecutionAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointment.Id,
                It.IsAny<StartServiceAppointmentExecutionRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(true, appointment));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());

        var result = await controller.StartExecution(
            appointment.Id,
            new StartServiceAppointmentExecutionRequestDto("Inicio"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ServiceAppointmentDto>(ok.Value);
        Assert.Equal(ServiceAppointmentStatus.InProgress.ToString(), dto.Status);
    }

    [Fact]
    public async Task RespondPresence_ShouldReturnOk_WhenServiceSucceeds()
    {
        var appointmentId = Guid.NewGuid();
        var appointment = new ServiceAppointmentDto(
            appointmentId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ServiceAppointmentStatus.Confirmed.ToString(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(2),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            Array.Empty<ServiceAppointmentHistoryDto>(),
            ClientPresenceConfirmed: true,
            ClientPresenceRespondedAtUtc: DateTime.UtcNow);

        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.RespondPresenceAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                It.IsAny<RespondServiceAppointmentPresenceRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(true, appointment));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Client.ToString());

        var result = await controller.RespondPresence(
            appointmentId,
            new RespondServiceAppointmentPresenceRequestDto(true, "Confirmado"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ServiceAppointmentDto>(ok.Value);
        Assert.Equal(appointmentId, dto.Id);
        Assert.True(dto.ClientPresenceConfirmed);
    }

    [Fact]
    public async Task UpdateOperationalStatus_ShouldReturnConflict_WhenTransitionIsInvalid()
    {
        var appointmentId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.UpdateOperationalStatusAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                It.IsAny<UpdateServiceAppointmentOperationalStatusRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "invalid_operational_transition",
                ErrorMessage: "Transicao invalida."));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());

        var result = await controller.UpdateOperationalStatus(
            appointmentId,
            new UpdateServiceAppointmentOperationalStatusRequestDto("InService", "Teste"));

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task GetChecklist_ShouldReturnOk_WhenChecklistExists()
    {
        var appointmentId = Guid.NewGuid();
        var checklistServiceMock = new Mock<IServiceAppointmentChecklistService>();
        checklistServiceMock
            .Setup(s => s.GetChecklistAsync(It.IsAny<Guid>(), It.IsAny<string>(), appointmentId))
            .ReturnsAsync(new ServiceAppointmentChecklistResultDto(
                true,
                new ServiceAppointmentChecklistDto(
                    appointmentId,
                    Guid.NewGuid(),
                    "Eletrica - padrao",
                    "Eletrica",
                    true,
                    2,
                    1,
                    Array.Empty<ServiceChecklistItemDto>(),
                    Array.Empty<ServiceChecklistHistoryDto>())));

        var controller = CreateController(
            Mock.Of<IServiceAppointmentService>(),
            Guid.NewGuid(),
            UserRole.Provider.ToString(),
            checklistServiceMock.Object);

        var result = await controller.GetChecklist(appointmentId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ServiceAppointmentChecklistDto>(ok.Value);
        Assert.Equal(appointmentId, dto.AppointmentId);
    }

    [Fact]
    public async Task UpsertChecklistItem_ShouldReturnConflict_WhenEvidenceIsRequired()
    {
        var appointmentId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var checklistServiceMock = new Mock<IServiceAppointmentChecklistService>();
        checklistServiceMock
            .Setup(s => s.UpsertItemResponseAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                It.IsAny<UpsertServiceChecklistItemResponseRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentChecklistResultDto(
                false,
                ErrorCode: "evidence_required",
                ErrorMessage: "Item exige evidencia."));

        var controller = CreateController(
            Mock.Of<IServiceAppointmentService>(),
            Guid.NewGuid(),
            UserRole.Provider.ToString(),
            checklistServiceMock.Object);

        var result = await controller.UpsertChecklistItem(
            appointmentId,
            itemId,
            new UpsertServiceChecklistItemResponseRequestDto(
                itemId,
                true,
                "Teste",
                null,
                null,
                null,
                null,
                false));

        Assert.IsType<ConflictObjectResult>(result);
    }

    private static ServiceAppointmentsController CreateController(
        IServiceAppointmentService service,
        Guid? userId = null,
        string? role = null,
        IServiceAppointmentChecklistService? checklistService = null)
    {
        checklistService ??= Mock.Of<IServiceAppointmentChecklistService>();

        var controller = new ServiceAppointmentsController(service, checklistService)
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
