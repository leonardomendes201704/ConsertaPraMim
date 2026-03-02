using System.Security.Claims;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Web.Client.Controllers;
using ConsertaPraMim.Web.Client.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class ClientServiceRequestsCreateReviewGateControllerTests
{
    [Fact(DisplayName = "Cliente service requests create | Deve preparar bloqueio quando houver avaliacao pendente")]
    public async Task Create_Get_ShouldPrepareReviewGate_WhenClientHasPendingReviews()
    {
        var userId = Guid.NewGuid();
        var reviewServiceMock = new Mock<IReviewService>();
        reviewServiceMock
            .Setup(service => service.GetPendingClientReviewsAsync(userId, It.IsAny<int>()))
            .ReturnsAsync(new[] { BuildPendingReview() });

        var controller = CreateController(
            reviewService: reviewServiceMock.Object,
            userId: userId);

        var result = await controller.Create();

        Assert.IsType<ViewResult>(result);
        Assert.True((bool)controller.ViewBag.PendingReviewCreateBlocked);

        var pendingReviews = Assert.IsAssignableFrom<IReadOnlyList<ReviewPendingRequestDto>>(controller.ViewBag.PendingClientReviews);
        Assert.Single(pendingReviews);
    }

    [Fact(DisplayName = "Cliente service requests create | Deve impedir criacao quando houver avaliacao pendente")]
    public async Task Create_Post_ShouldBlock_WhenClientHasPendingReviews()
    {
        var userId = Guid.NewGuid();
        var requestServiceMock = new Mock<IServiceRequestService>();
        var reviewServiceMock = new Mock<IReviewService>();
        reviewServiceMock
            .Setup(service => service.GetPendingClientReviewsAsync(userId, It.IsAny<int>()))
            .ReturnsAsync(new[] { BuildPendingReview() });

        var controller = CreateController(
            requestService: requestServiceMock.Object,
            reviewService: reviewServiceMock.Object,
            userId: userId);

        var dto = new CreateServiceRequestDto(
            Guid.NewGuid(),
            null,
            "Troca de tomada na sala com urgencia.",
            "Rua A",
            "Praia Grande",
            "11704-150",
            -24.01,
            -46.41);

        var result = await controller.Create(dto);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(dto, view.Model);
        Assert.True((bool)controller.ViewBag.PendingReviewCreateBlocked);
        requestServiceMock.Verify(
            service => service.CreateAsync(It.IsAny<Guid>(), It.IsAny<CreateServiceRequestDto>()),
            Times.Never);
    }

    [Fact(DisplayName = "Cliente service requests create | Deve retornar fila remanescente apos avaliar no modal")]
    public async Task SubmitPendingProviderReviewForCreate_ShouldReturnRemainingQueue_WhenReviewIsSubmitted()
    {
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var reviewServiceMock = new Mock<IReviewService>();
        reviewServiceMock
            .Setup(service => service.SubmitClientReviewDetailedAsync(
                userId,
                It.Is<CreateReviewDto>(dto => dto.RequestId == requestId && dto.Rating == 5)))
            .ReturnsAsync(new ReviewSubmissionResultDto(true));
        reviewServiceMock
            .Setup(service => service.GetPendingClientReviewsAsync(userId, It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<ReviewPendingRequestDto>());

        var controller = CreateController(
            reviewService: reviewServiceMock.Object,
            userId: userId);

        var result = await controller.SubmitPendingProviderReviewForCreate(requestId, 5, "Atendimento excelente.");

        var json = Assert.IsType<JsonResult>(result);
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(json.Value));
        Assert.True(payload.RootElement.GetProperty("success").GetBoolean());
        Assert.False(payload.RootElement.GetProperty("hasBlockingPendingReviews").GetBoolean());
        Assert.Equal(0, payload.RootElement.GetProperty("remainingPendingReviews").GetArrayLength());
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
        IClientSupportTicketService? clientSupportTicketService = null,
        IFileStorageService? fileStorageService = null,
        Guid? userId = null)
    {
        requestService ??= Mock.Of<IServiceRequestService>();
        proposalService ??= Mock.Of<IProposalService>();
        providerGalleryService ??= Mock.Of<IProviderGalleryService>();
        zipGeocodingService ??= Mock.Of<IZipGeocodingService>();
        appointmentService ??= Mock.Of<IServiceAppointmentService>();
        appointmentChecklistService ??= Mock.Of<IServiceAppointmentChecklistService>();
        reviewService ??= Mock.Of<IReviewService>();
        clientSupportTicketService ??= Mock.Of<IClientSupportTicketService>();
        fileStorageService ??= Mock.Of<IFileStorageService>();

        if (categoryCatalogService == null)
        {
            var categoryCatalogServiceMock = new Mock<IServiceCategoryCatalogService>();
            categoryCatalogServiceMock
                .Setup(service => service.GetActiveAsync())
                .ReturnsAsync(Array.Empty<ServiceCategoryOptionDto>());
            categoryCatalogService = categoryCatalogServiceMock.Object;
        }

        var httpContextAccessor = new HttpContextAccessor();
        var clientApiCaller = new ClientApiCaller(
            Mock.Of<IHttpClientFactory>(),
            httpContextAccessor,
            new ConfigurationBuilder().Build(),
            Mock.Of<ILogger<ClientApiCaller>>());

        var controller = new ServiceRequestsController(
            requestService,
            categoryCatalogService,
            proposalService,
            providerGalleryService,
            zipGeocodingService,
            appointmentService,
            appointmentChecklistService,
            reviewService,
            clientSupportTicketService,
            fileStorageService,
            clientApiCaller)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        httpContextAccessor.HttpContext = controller.ControllerContext.HttpContext;

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

    private static ReviewPendingRequestDto BuildPendingReview()
    {
        return new ReviewPendingRequestDto(
            Guid.NewGuid(),
            "Prestador Exemplo",
            "Provider",
            "Hidraulica",
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow.AddDays(5),
            5);
    }
}
