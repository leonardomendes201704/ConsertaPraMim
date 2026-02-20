using System.Security.Claims;
using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using ConsertaPraMim.Infrastructure.Hubs;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integration.E2E;

public class SupportTicketsProviderAdminE2EInMemoryIntegrationTests
{
    [Fact]
    public async Task ProviderAndAdmin_ShouldCompleteTicketLifecycle_EndToEnd()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var provider = CreateUser("provider.e2e.support@teste.com", UserRole.Provider);
        var adminA = CreateUser("admin.e2e.a@teste.com", UserRole.Admin);
        var adminB = CreateUser("admin.e2e.b@teste.com", UserRole.Admin);
        context.Users.AddRange(provider, adminA, adminB);
        await context.SaveChangesAsync();

        var mobileService = BuildMobileProviderService(context);
        var adminService = BuildAdminSupportService(context);

        var providerController = BuildProviderController(mobileService, provider.Id);
        var adminController = BuildAdminController(adminService, adminA);

        var createResult = await providerController.CreateSupportTicket(new MobileProviderCreateSupportTicketRequestDto(
            "Falha intermitente no app",
            "Technical",
            (int)SupportTicketPriority.High,
            "Aplicativo fecha ao abrir agenda."));
        var createdAction = Assert.IsType<CreatedAtActionResult>(createResult);
        var createdTicket = Assert.IsType<MobileProviderSupportTicketDetailsDto>(createdAction.Value);
        var ticketId = createdTicket.Ticket.Id;

        var listResult = await adminController.GetTickets();
        var listOk = Assert.IsType<OkObjectResult>(listResult);
        var listPayload = Assert.IsType<AdminSupportTicketListResponseDto>(listOk.Value);
        Assert.Contains(listPayload.Items, item => item.Id == ticketId);

        var assignResult = await adminController.Assign(
            ticketId,
            new AdminSupportTicketAssignRequestDto(adminB.Id, "Escalado para plantao."));
        var assignOk = Assert.IsType<OkObjectResult>(assignResult);
        var assignedDetails = Assert.IsType<AdminSupportTicketDetailsDto>(assignOk.Value);
        Assert.Equal(adminB.Id, assignedDetails.Ticket.AssignedAdminUserId);

        var replyResult = await adminController.AddMessage(
            ticketId,
            new AdminSupportTicketMessageRequestDto("Estamos analisando o incidente."));
        var replyOk = Assert.IsType<OkObjectResult>(replyResult);
        var repliedDetails = Assert.IsType<AdminSupportTicketDetailsDto>(replyOk.Value);
        Assert.Equal(SupportTicketStatus.WaitingProvider.ToString(), repliedDetails.Ticket.Status);

        var providerDetailsResult = await providerController.GetSupportTicketDetails(ticketId);
        var providerDetailsOk = Assert.IsType<OkObjectResult>(providerDetailsResult);
        var providerDetails = Assert.IsType<MobileProviderSupportTicketDetailsDto>(providerDetailsOk.Value);
        Assert.Equal(2, providerDetails.Messages.Count);

        var closeResult = await adminController.UpdateStatus(
            ticketId,
            new AdminSupportTicketStatusUpdateRequestDto(SupportTicketStatus.Closed.ToString(), "Encerrando para validacao."));
        var closeOk = Assert.IsType<OkObjectResult>(closeResult);
        var closedDetails = Assert.IsType<AdminSupportTicketDetailsDto>(closeOk.Value);
        Assert.Equal(SupportTicketStatus.Closed.ToString(), closedDetails.Ticket.Status);

        var reopenResult = await adminController.UpdateStatus(
            ticketId,
            new AdminSupportTicketStatusUpdateRequestDto(SupportTicketStatus.InProgress.ToString(), "Reaberto por novo relato."));
        var reopenOk = Assert.IsType<OkObjectResult>(reopenResult);
        var reopenedDetails = Assert.IsType<AdminSupportTicketDetailsDto>(reopenOk.Value);
        Assert.Equal(SupportTicketStatus.InProgress.ToString(), reopenedDetails.Ticket.Status);

        var providerCloseResult = await providerController.CloseSupportTicket(ticketId);
        var providerCloseOk = Assert.IsType<OkObjectResult>(providerCloseResult);
        var providerClosed = Assert.IsType<MobileProviderSupportTicketDetailsDto>(providerCloseOk.Value);
        Assert.Equal(SupportTicketStatus.Closed.ToString(), providerClosed.Ticket.Status);
    }

    [Fact]
    public async Task ProviderIsolation_ShouldBlockAccessToForeignTicket_EndToEnd()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var ownerProvider = CreateUser("provider.owner.e2e@teste.com", UserRole.Provider);
        var foreignProvider = CreateUser("provider.foreign.e2e@teste.com", UserRole.Provider);
        context.Users.AddRange(ownerProvider, foreignProvider);
        await context.SaveChangesAsync();

        var mobileService = BuildMobileProviderService(context);
        var ownerController = BuildProviderController(mobileService, ownerProvider.Id);
        var foreignController = BuildProviderController(mobileService, foreignProvider.Id);

        var createResult = await ownerController.CreateSupportTicket(new MobileProviderCreateSupportTicketRequestDto(
            "Ticket privado e2e",
            "General",
            (int)SupportTicketPriority.Medium,
            "Somente o dono deve acessar."));
        var createdAction = Assert.IsType<CreatedAtActionResult>(createResult);
        var createdTicket = Assert.IsType<MobileProviderSupportTicketDetailsDto>(createdAction.Value);

        var detailsResult = await foreignController.GetSupportTicketDetails(createdTicket.Ticket.Id);
        var messageResult = await foreignController.AddSupportTicketMessage(
            createdTicket.Ticket.Id,
            new MobileProviderSupportTicketMessageRequestDto("Tentativa indevida"));
        var closeResult = await foreignController.CloseSupportTicket(createdTicket.Ticket.Id);

        Assert.IsType<NotFoundObjectResult>(detailsResult);
        Assert.IsType<NotFoundObjectResult>(messageResult);
        Assert.IsType<NotFoundObjectResult>(closeResult);
    }

    private static MobileProviderService BuildMobileProviderService(ConsertaPraMimDbContext context)
    {
        return new MobileProviderService(
            Mock.Of<IServiceRequestService>(),
            Mock.Of<IProposalService>(),
            Mock.Of<IServiceAppointmentService>(),
            Mock.Of<IServiceAppointmentChecklistService>(),
            Mock.Of<IChatService>(),
            Mock.Of<IProfileService>(),
            Mock.Of<IUserPresenceTracker>(),
            new UserRepository(context),
            Mock.Of<IServiceCategoryRepository>(),
            new SupportTicketRepository(context));
    }

    private static AdminSupportTicketService BuildAdminSupportService(ConsertaPraMimDbContext context)
    {
        return new AdminSupportTicketService(
            new SupportTicketRepository(context),
            new UserRepository(context),
            new AdminAuditLogRepository(context));
    }

    private static MobileProviderController BuildProviderController(IMobileProviderService service, Guid providerId)
    {
        var controller = new MobileProviderController(
            service,
            Mock.Of<IFileStorageService>(),
            Mock.Of<IChatService>(),
            Mock.Of<IZipGeocodingService>(),
            Mock.Of<IHubContext<ChatHub>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, providerId.ToString()),
            new Claim(ClaimTypes.Role, UserRole.Provider.ToString())
        ], "TestAuth"));

        return controller;
    }

    private static AdminSupportTicketsController BuildAdminController(IAdminSupportTicketService service, User adminUser)
    {
        var controller = new AdminSupportTicketsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, adminUser.Id.ToString()),
            new Claim(ClaimTypes.Email, adminUser.Email),
            new Claim(ClaimTypes.Role, UserRole.Admin.ToString())
        ], "TestAuth"));

        return controller;
    }

    private static User CreateUser(string email, UserRole role)
    {
        return new User
        {
            Name = email.Split('@')[0],
            Email = email,
            PasswordHash = "hash",
            Phone = "11999999999",
            Role = role,
            IsActive = true
        };
    }
}
