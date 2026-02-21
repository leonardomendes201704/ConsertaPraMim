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

namespace ConsertaPraMim.Tests.Unit.Integration.Controllers;

public class MobileProviderSupportTicketsControllerInMemoryIntegrationTests
{
    [Fact(DisplayName = "Mobile prestador support tickets controller em memory integracao | Criar listar e details | Deve retornar expected contracts")]
    public async Task CreateListAndDetails_ShouldReturnExpectedContracts()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var provider = CreateUser("provider.controller@teste.com", UserRole.Provider);
        context.Users.Add(provider);
        await context.SaveChangesAsync();

        var service = BuildService(context);
        var controller = BuildController(service, provider.Id);

        var create = await controller.CreateSupportTicket(new MobileProviderCreateSupportTicketRequestDto(
            "Erro de pagamento",
            "Billing",
            (int)SupportTicketPriority.High,
            "Pagamento nao caiu."));

        var created = Assert.IsType<CreatedAtActionResult>(create);
        var createdPayload = Assert.IsType<MobileProviderSupportTicketDetailsDto>(created.Value);
        var ticketId = createdPayload.Ticket.Id;

        var list = await controller.GetSupportTickets(page: 1, pageSize: 20);
        var listOk = Assert.IsType<OkObjectResult>(list);
        var listPayload = Assert.IsType<MobileProviderSupportTicketListResponseDto>(listOk.Value);
        Assert.Single(listPayload.Items);
        Assert.Equal(ticketId, listPayload.Items[0].Id);

        var details = await controller.GetSupportTicketDetails(ticketId);
        var detailsOk = Assert.IsType<OkObjectResult>(details);
        var detailsPayload = Assert.IsType<MobileProviderSupportTicketDetailsDto>(detailsOk.Value);
        Assert.Equal(ticketId, detailsPayload.Ticket.Id);
    }

    [Fact(DisplayName = "Mobile prestador support tickets controller em memory integracao | Obter details | Deve retornar nao encontrado for foreign ticket")]
    public async Task GetDetails_ShouldReturnNotFound_ForForeignTicket()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var providerA = CreateUser("provider.a.controller@teste.com", UserRole.Provider);
        var providerB = CreateUser("provider.b.controller@teste.com", UserRole.Provider);
        context.Users.AddRange(providerA, providerB);
        await context.SaveChangesAsync();

        var service = BuildService(context);
        var ownerController = BuildController(service, providerB.Id);
        var foreignController = BuildController(service, providerA.Id);

        var create = await ownerController.CreateSupportTicket(new MobileProviderCreateSupportTicketRequestDto(
            "Ticket privado",
            "General",
            (int)SupportTicketPriority.Medium,
            "Nao deve ser acessivel por outro prestador."));
        var created = Assert.IsType<CreatedAtActionResult>(create);
        var createdPayload = Assert.IsType<MobileProviderSupportTicketDetailsDto>(created.Value);

        var details = await foreignController.GetSupportTicketDetails(createdPayload.Ticket.Id);
        Assert.IsType<NotFoundObjectResult>(details);
    }

    private static MobileProviderController BuildController(IMobileProviderService service, Guid providerId)
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

    private static MobileProviderService BuildService(ConsertaPraMimDbContext context)
    {
        var repository = new SupportTicketRepository(context);
        return new MobileProviderService(
            Mock.Of<IServiceRequestService>(),
            Mock.Of<IProposalService>(),
            Mock.Of<IServiceAppointmentService>(),
            Mock.Of<IServiceAppointmentChecklistService>(),
            Mock.Of<IChatService>(),
            Mock.Of<IProfileService>(),
            Mock.Of<IUserPresenceTracker>(),
            Mock.Of<IUserRepository>(),
            Mock.Of<IServiceCategoryRepository>(),
            repository);
    }

    private static User CreateUser(string email, UserRole role)
    {
        return new User
        {
            Name = email.Split('@')[0],
            Email = email,
            PasswordHash = "hash",
            Phone = "11999999999",
            Role = role
        };
    }
}
