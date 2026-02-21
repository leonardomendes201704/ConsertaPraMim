using System.Security.Claims;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Web.Client.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class ClientServiceRequestsSchedulingControllerTests
{
    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Cliente servico requisicoes scheduling controller | Slots | Deve retornar nao autorizado quando usuario missing.
    /// </summary>
    [Fact(DisplayName = "Cliente servico requisicoes scheduling controller | Slots | Deve retornar nao autorizado quando usuario missing")]
    public async Task Slots_ShouldReturnUnauthorized_WhenUserIsMissing()
    {
        var controller = CreateController();

        var result = await controller.Slots(Guid.NewGuid(), Guid.NewGuid(), "2026-02-16");

        Assert.IsType<UnauthorizedResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Cliente servico requisicoes scheduling controller | Slots | Deve retornar nao encontrado quando requisicao nao exist.
    /// </summary>
    [Fact(DisplayName = "Cliente servico requisicoes scheduling controller | Slots | Deve retornar nao encontrado quando requisicao nao exist")]
    public async Task Slots_ShouldReturnNotFound_WhenRequestDoesNotExist()
    {
        var requestServiceMock = new Mock<IServiceRequestService>();
        requestServiceMock
            .Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), UserRole.Client.ToString()))
            .ReturnsAsync((ServiceRequestDto?)null);

        var controller = CreateController(
            requestService: requestServiceMock.Object,
            userId: Guid.NewGuid());

        var result = await controller.Slots(Guid.NewGuid(), Guid.NewGuid(), "2026-02-16");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Cliente servico requisicoes scheduling controller | Slots | Deve retornar conflito quando prestador tem no accepted proposal.
    /// </summary>
    [Fact(DisplayName = "Cliente servico requisicoes scheduling controller | Slots | Deve retornar conflito quando prestador tem no accepted proposal")]
    public async Task Slots_ShouldReturnConflict_WhenProviderHasNoAcceptedProposal()
    {
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var requestServiceMock = new Mock<IServiceRequestService>();
        requestServiceMock
            .Setup(s => s.GetByIdAsync(requestId, userId, UserRole.Client.ToString()))
            .ReturnsAsync(BuildRequestDto(requestId));

        var proposalServiceMock = new Mock<IProposalService>();
        proposalServiceMock
            .Setup(s => s.GetByRequestAsync(requestId, userId, UserRole.Client.ToString()))
            .ReturnsAsync(new List<ProposalDto>
            {
                new(Guid.NewGuid(), requestId, Guid.NewGuid(), "Prestador 99", 150m, false, null, DateTime.UtcNow)
            });

        var controller = CreateController(
            requestService: requestServiceMock.Object,
            proposalService: proposalServiceMock.Object,
            userId: userId);

        var result = await controller.Slots(requestId, providerId, "2026-02-16");

        Assert.IsType<ConflictObjectResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Cliente servico requisicoes scheduling controller | Slots | Deve retornar invalida requisicao quando date invalido.
    /// </summary>
    [Fact(DisplayName = "Cliente servico requisicoes scheduling controller | Slots | Deve retornar invalida requisicao quando date invalido")]
    public async Task Slots_ShouldReturnBadRequest_WhenDateIsInvalid()
    {
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var requestServiceMock = new Mock<IServiceRequestService>();
        requestServiceMock
            .Setup(s => s.GetByIdAsync(requestId, userId, UserRole.Client.ToString()))
            .ReturnsAsync(BuildRequestDto(requestId));

        var proposalServiceMock = new Mock<IProposalService>();
        proposalServiceMock
            .Setup(s => s.GetByRequestAsync(requestId, userId, UserRole.Client.ToString()))
            .ReturnsAsync(new List<ProposalDto>
            {
                new(Guid.NewGuid(), requestId, providerId, "Prestador 01", 180m, true, null, DateTime.UtcNow)
            });

        var controller = CreateController(
            requestService: requestServiceMock.Object,
            proposalService: proposalServiceMock.Object,
            userId: userId);

        var result = await controller.Slots(requestId, providerId, "16/02/2026");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Cliente servico requisicoes scheduling controller | Slots | Deve retornar json com slots quando flow valido.
    /// </summary>
    [Fact(DisplayName = "Cliente servico requisicoes scheduling controller | Slots | Deve retornar json com slots quando flow valido")]
    public async Task Slots_ShouldReturnJsonWithSlots_WhenFlowIsValid()
    {
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        GetServiceAppointmentSlotsQueryDto? capturedQuery = null;

        var requestServiceMock = new Mock<IServiceRequestService>();
        requestServiceMock
            .Setup(s => s.GetByIdAsync(requestId, userId, UserRole.Client.ToString()))
            .ReturnsAsync(BuildRequestDto(requestId));

        var proposalServiceMock = new Mock<IProposalService>();
        proposalServiceMock
            .Setup(s => s.GetByRequestAsync(requestId, userId, UserRole.Client.ToString()))
            .ReturnsAsync(new List<ProposalDto>
            {
                new(Guid.NewGuid(), requestId, providerId, "Prestador 01", 180m, true, null, DateTime.UtcNow)
            });

        var slotStart = DateTime.UtcNow.Date.AddDays(1).AddHours(10);
        var slotEnd = slotStart.AddHours(1);
        var appointmentServiceMock = new Mock<IServiceAppointmentService>();
        appointmentServiceMock
            .Setup(s => s.GetAvailableSlotsAsync(
                userId,
                UserRole.Client.ToString(),
                It.IsAny<GetServiceAppointmentSlotsQueryDto>()))
            .Callback<Guid, string, GetServiceAppointmentSlotsQueryDto>((_, _, query) => capturedQuery = query)
            .ReturnsAsync(new ServiceAppointmentSlotsResultDto(
                true,
                new List<ServiceAppointmentSlotDto> { new(slotStart, slotEnd) }));

        var controller = CreateController(
            requestService: requestServiceMock.Object,
            proposalService: proposalServiceMock.Object,
            appointmentService: appointmentServiceMock.Object,
            userId: userId);

        var result = await controller.Slots(requestId, providerId, "2026-02-16");

        var json = Assert.IsType<JsonResult>(result);
        var payload = JsonDocument.Parse(JsonSerializer.Serialize(json.Value));
        var slots = payload.RootElement.GetProperty("slots");
        Assert.Equal(1, slots.GetArrayLength());
        Assert.Equal(slotStart, slots[0].GetProperty("windowStartUtc").GetDateTime());
        Assert.Equal(slotEnd, slots[0].GetProperty("windowEndUtc").GetDateTime());

        Assert.NotNull(capturedQuery);
        Assert.Equal(providerId, capturedQuery!.ProviderId);
        Assert.Equal(TimeSpan.FromDays(1), capturedQuery.ToUtc - capturedQuery.FromUtc);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Cliente servico requisicoes scheduling controller | Criar appointment | Deve retornar invalida requisicao quando ids missing.
    /// </summary>
    [Fact(DisplayName = "Cliente servico requisicoes scheduling controller | Criar appointment | Deve retornar invalida requisicao quando ids missing")]
    public async Task CreateAppointment_ShouldReturnBadRequest_WhenIdsAreMissing()
    {
        var controller = CreateController(userId: Guid.NewGuid());

        var result = await controller.CreateAppointment(new ServiceRequestsController.CreateAppointmentInput(
            Guid.Empty,
            Guid.Empty,
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(3),
            null));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Cliente servico requisicoes scheduling controller | Criar appointment | Deve retornar conflito quando slot unavailable.
    /// </summary>
    [Fact(DisplayName = "Cliente servico requisicoes scheduling controller | Criar appointment | Deve retornar conflito quando slot unavailable")]
    public async Task CreateAppointment_ShouldReturnConflict_WhenSlotIsUnavailable()
    {
        var userId = Guid.NewGuid();
        var appointmentServiceMock = new Mock<IServiceAppointmentService>();
        appointmentServiceMock
            .Setup(s => s.CreateAsync(
                userId,
                UserRole.Client.ToString(),
                It.IsAny<CreateServiceAppointmentRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "slot_unavailable",
                ErrorMessage: "Janela indisponivel."));

        var controller = CreateController(
            appointmentService: appointmentServiceMock.Object,
            userId: userId);

        var result = await controller.CreateAppointment(new ServiceRequestsController.CreateAppointmentInput(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(3),
            "Teste"));

        Assert.IsType<ConflictObjectResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Cliente servico requisicoes scheduling controller | Criar appointment | Deve retornar ok quando servico sucesso.
    /// </summary>
    [Fact(DisplayName = "Cliente servico requisicoes scheduling controller | Criar appointment | Deve retornar ok quando servico sucesso")]
    public async Task CreateAppointment_ShouldReturnOk_WhenServiceSucceeds()
    {
        var userId = Guid.NewGuid();
        var appointment = BuildAppointmentDto(ServiceAppointmentStatus.PendingProviderConfirmation.ToString());
        var appointmentServiceMock = new Mock<IServiceAppointmentService>();
        appointmentServiceMock
            .Setup(s => s.CreateAsync(
                userId,
                UserRole.Client.ToString(),
                It.IsAny<CreateServiceAppointmentRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(true, appointment));

        var controller = CreateController(
            appointmentService: appointmentServiceMock.Object,
            userId: userId);

        var result = await controller.CreateAppointment(new ServiceRequestsController.CreateAppointmentInput(
            appointment.ServiceRequestId,
            appointment.ProviderId,
            appointment.WindowStartUtc,
            appointment.WindowEndUtc,
            "Agendamento inicial"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.True(payload.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(appointment.Id, payload.RootElement.GetProperty("appointment").GetProperty("id").GetGuid());
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Cliente servico requisicoes scheduling controller | Requisicao appointment reschedule | Deve retornar ok quando servico sucesso.
    /// </summary>
    [Fact(DisplayName = "Cliente servico requisicoes scheduling controller | Requisicao appointment reschedule | Deve retornar ok quando servico sucesso")]
    public async Task RequestAppointmentReschedule_ShouldReturnOk_WhenServiceSucceeds()
    {
        var userId = Guid.NewGuid();
        var appointment = BuildAppointmentDto(ServiceAppointmentStatus.RescheduleRequestedByClient.ToString());
        var appointmentServiceMock = new Mock<IServiceAppointmentService>();
        appointmentServiceMock
            .Setup(s => s.RequestRescheduleAsync(
                userId,
                UserRole.Client.ToString(),
                appointment.Id,
                It.IsAny<RequestServiceAppointmentRescheduleDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(true, appointment));

        var controller = CreateController(
            appointmentService: appointmentServiceMock.Object,
            userId: userId);

        var result = await controller.RequestAppointmentReschedule(
            new ServiceRequestsController.RequestRescheduleInput(
                appointment.Id,
                appointment.WindowStartUtc.AddHours(2),
                appointment.WindowEndUtc.AddHours(2),
                "Reagendar"));

        Assert.IsType<OkObjectResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Cliente servico requisicoes scheduling controller | Requisicao appointment reschedule | Deve retornar invalida requisicao quando appointment id missing.
    /// </summary>
    [Fact(DisplayName = "Cliente servico requisicoes scheduling controller | Requisicao appointment reschedule | Deve retornar invalida requisicao quando appointment id missing")]
    public async Task RequestAppointmentReschedule_ShouldReturnBadRequest_WhenAppointmentIdIsMissing()
    {
        var controller = CreateController(userId: Guid.NewGuid());

        var result = await controller.RequestAppointmentReschedule(
            new ServiceRequestsController.RequestRescheduleInput(
                Guid.Empty,
                DateTime.UtcNow.AddHours(2),
                DateTime.UtcNow.AddHours(3),
                "Reagendar"));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Cliente servico requisicoes scheduling controller | Respond appointment reschedule | Deve retornar ok quando servico sucesso.
    /// </summary>
    [Fact(DisplayName = "Cliente servico requisicoes scheduling controller | Respond appointment reschedule | Deve retornar ok quando servico sucesso")]
    public async Task RespondAppointmentReschedule_ShouldReturnOk_WhenServiceSucceeds()
    {
        var userId = Guid.NewGuid();
        var appointment = BuildAppointmentDto(ServiceAppointmentStatus.Confirmed.ToString());
        var appointmentServiceMock = new Mock<IServiceAppointmentService>();
        appointmentServiceMock
            .Setup(s => s.RespondRescheduleAsync(
                userId,
                UserRole.Client.ToString(),
                appointment.Id,
                It.IsAny<RespondServiceAppointmentRescheduleRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(true, appointment));

        var controller = CreateController(
            appointmentService: appointmentServiceMock.Object,
            userId: userId);

        var result = await controller.RespondAppointmentReschedule(
            new ServiceRequestsController.RespondRescheduleInput(appointment.Id, true, "Aceito"));

        Assert.IsType<OkObjectResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Cliente servico requisicoes scheduling controller | Respond appointment reschedule | Deve retornar invalida requisicao quando appointment id missing.
    /// </summary>
    [Fact(DisplayName = "Cliente servico requisicoes scheduling controller | Respond appointment reschedule | Deve retornar invalida requisicao quando appointment id missing")]
    public async Task RespondAppointmentReschedule_ShouldReturnBadRequest_WhenAppointmentIdIsMissing()
    {
        var controller = CreateController(userId: Guid.NewGuid());

        var result = await controller.RespondAppointmentReschedule(
            new ServiceRequestsController.RespondRescheduleInput(Guid.Empty, true, "Aceito"));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Cliente servico requisicoes scheduling controller | Cancelar appointment | Deve retornar ok quando servico sucesso.
    /// </summary>
    [Fact(DisplayName = "Cliente servico requisicoes scheduling controller | Cancelar appointment | Deve retornar ok quando servico sucesso")]
    public async Task CancelAppointment_ShouldReturnOk_WhenServiceSucceeds()
    {
        var userId = Guid.NewGuid();
        var appointment = BuildAppointmentDto(ServiceAppointmentStatus.CancelledByClient.ToString());
        var appointmentServiceMock = new Mock<IServiceAppointmentService>();
        appointmentServiceMock
            .Setup(s => s.CancelAsync(
                userId,
                UserRole.Client.ToString(),
                appointment.Id,
                It.IsAny<CancelServiceAppointmentRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(true, appointment));

        var controller = CreateController(
            appointmentService: appointmentServiceMock.Object,
            userId: userId);

        var result = await controller.CancelAppointment(
            new ServiceRequestsController.CancelAppointmentInput(appointment.Id, "Nao estarei no local"));

        Assert.IsType<OkObjectResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Cliente servico requisicoes scheduling controller | Cancelar appointment | Deve retornar invalida requisicao quando appointment id missing.
    /// </summary>
    [Fact(DisplayName = "Cliente servico requisicoes scheduling controller | Cancelar appointment | Deve retornar invalida requisicao quando appointment id missing")]
    public async Task CancelAppointment_ShouldReturnBadRequest_WhenAppointmentIdIsMissing()
    {
        var controller = CreateController(userId: Guid.NewGuid());

        var result = await controller.CancelAppointment(
            new ServiceRequestsController.CancelAppointmentInput(Guid.Empty, "Teste"));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Cliente servico requisicoes scheduling controller | Respond appointment presence | Deve retornar ok quando servico sucesso.
    /// </summary>
    [Fact(DisplayName = "Cliente servico requisicoes scheduling controller | Respond appointment presence | Deve retornar ok quando servico sucesso")]
    public async Task RespondAppointmentPresence_ShouldReturnOk_WhenServiceSucceeds()
    {
        var userId = Guid.NewGuid();
        var appointment = BuildAppointmentDto(ServiceAppointmentStatus.Confirmed.ToString()) with
        {
            ClientPresenceConfirmed = true
        };

        var appointmentServiceMock = new Mock<IServiceAppointmentService>();
        appointmentServiceMock
            .Setup(s => s.RespondPresenceAsync(
                userId,
                UserRole.Client.ToString(),
                appointment.Id,
                It.IsAny<RespondServiceAppointmentPresenceRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(true, appointment));

        var controller = CreateController(
            appointmentService: appointmentServiceMock.Object,
            userId: userId);

        var result = await controller.RespondAppointmentPresence(
            new ServiceRequestsController.RespondPresenceInput(appointment.Id, true, "Confirmo"));

        Assert.IsType<OkObjectResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Cliente servico requisicoes scheduling controller | Respond appointment presence | Deve retornar invalida requisicao quando appointment id missing.
    /// </summary>
    [Fact(DisplayName = "Cliente servico requisicoes scheduling controller | Respond appointment presence | Deve retornar invalida requisicao quando appointment id missing")]
    public async Task RespondAppointmentPresence_ShouldReturnBadRequest_WhenAppointmentIdIsMissing()
    {
        var controller = CreateController(userId: Guid.NewGuid());

        var result = await controller.RespondAppointmentPresence(
            new ServiceRequestsController.RespondPresenceInput(Guid.Empty, true, "Confirmo"));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static ServiceRequestsController CreateController(
        IServiceRequestService? requestService = null,
        IServiceCategoryCatalogService? categoryCatalogService = null,
        IProposalService? proposalService = null,
        IProviderGalleryService? providerGalleryService = null,
        IZipGeocodingService? zipGeocodingService = null,
        IServiceAppointmentService? appointmentService = null,
        IServiceAppointmentChecklistService? appointmentChecklistService = null,
        IReviewService? reviewService = null,
        Guid? userId = null)
    {
        requestService ??= Mock.Of<IServiceRequestService>();
        categoryCatalogService ??= Mock.Of<IServiceCategoryCatalogService>();
        proposalService ??= Mock.Of<IProposalService>();
        providerGalleryService ??= Mock.Of<IProviderGalleryService>();
        zipGeocodingService ??= Mock.Of<IZipGeocodingService>();
        appointmentService ??= Mock.Of<IServiceAppointmentService>();
        appointmentChecklistService ??= Mock.Of<IServiceAppointmentChecklistService>();
        reviewService ??= Mock.Of<IReviewService>();

        var controller = new ServiceRequestsController(
            requestService,
            categoryCatalogService,
            proposalService,
            providerGalleryService,
            zipGeocodingService,
            appointmentService,
            appointmentChecklistService,
            reviewService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        if (userId.HasValue)
        {
            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
                        new Claim(ClaimTypes.Role, UserRole.Client.ToString())
                    },
                    "TestAuth"));
        }

        return controller;
    }

    private static ServiceRequestDto BuildRequestDto(Guid requestId)
    {
        return new ServiceRequestDto(
            requestId,
            ServiceRequestStatus.Created.ToString(),
            ServiceCategory.Electrical.ToString(),
            "Troca de fiacao",
            DateTime.UtcNow.AddDays(-1),
            "Rua A",
            "Santos",
            "11000-000");
    }

    private static ServiceAppointmentDto BuildAppointmentDto(string status)
    {
        var start = DateTime.UtcNow.AddHours(2);
        var end = start.AddHours(1);

        return new ServiceAppointmentDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            status,
            start,
            end,
            DateTime.UtcNow.AddHours(1),
            "Motivo",
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            Array.Empty<ServiceAppointmentHistoryDto>());
    }
}
