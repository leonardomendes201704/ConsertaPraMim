using System.Security.Claims;
using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Data;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integration.Controllers;

public class AdminSupportTicketsControllerInMemoryIntegrationTests
{
    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin support tickets controller em memory integracao | End para end admin flow | Deve listar assign reply e fechar.
    /// </summary>
    [Fact(DisplayName = "Admin support tickets controller em memory integracao | End para end admin flow | Deve listar assign reply e fechar")]
    public async Task EndToEndAdminFlow_ShouldListAssignReplyAndClose()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var provider = CreateUser("provider.controller.admin@teste.com", UserRole.Provider);
        var adminA = CreateUser("admin.controller.a@teste.com", UserRole.Admin);
        var adminB = CreateUser("admin.controller.b@teste.com", UserRole.Admin);
        context.Users.AddRange(provider, adminA, adminB);
        await context.SaveChangesAsync();

        var ticket = new SupportTicket
        {
            ProviderId = provider.Id,
            Subject = "Preciso ajuda",
            Category = "General",
            Priority = SupportTicketPriority.High,
            Status = SupportTicketStatus.Open,
            OpenedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            LastInteractionAtUtc = DateTime.UtcNow.AddMinutes(-20)
        };
        ticket.AddMessage(provider.Id, UserRole.Provider, "Mensagem inicial do prestador.");
        context.SupportTickets.Add(ticket);
        await context.SaveChangesAsync();

        var service = BuildService(context);
        var controller = BuildController(service, adminA);

        var list = await controller.GetTickets();
        var listOk = Assert.IsType<OkObjectResult>(list);
        var listPayload = Assert.IsType<AdminSupportTicketListResponseDto>(listOk.Value);
        Assert.Single(listPayload.Items);

        var assign = await controller.Assign(
            ticket.Id,
            new AdminSupportTicketAssignRequestDto(adminB.Id, "Atribuindo para o plantao B."));
        var assignOk = Assert.IsType<OkObjectResult>(assign);
        var assignPayload = Assert.IsType<AdminSupportTicketDetailsDto>(assignOk.Value);
        Assert.Equal(adminB.Id, assignPayload.Ticket.AssignedAdminUserId);

        var reply = await controller.AddMessage(
            ticket.Id,
            new AdminSupportTicketMessageRequestDto("Seguimos analisando."));
        var replyOk = Assert.IsType<OkObjectResult>(reply);
        var replyPayload = Assert.IsType<AdminSupportTicketDetailsDto>(replyOk.Value);
        Assert.Equal(SupportTicketStatus.WaitingProvider.ToString(), replyPayload.Ticket.Status);

        var close = await controller.UpdateStatus(
            ticket.Id,
            new AdminSupportTicketStatusUpdateRequestDto(SupportTicketStatus.Closed.ToString(), "Encerrando."));
        var closeOk = Assert.IsType<OkObjectResult>(close);
        var closePayload = Assert.IsType<AdminSupportTicketDetailsDto>(closeOk.Value);
        Assert.Equal(SupportTicketStatus.Closed.ToString(), closePayload.Ticket.Status);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin support tickets controller em memory integracao | Obter ticket details | Deve retornar nao encontrado quando ticket nao exist.
    /// </summary>
    [Fact(DisplayName = "Admin support tickets controller em memory integracao | Obter ticket details | Deve retornar nao encontrado quando ticket nao exist")]
    public async Task GetTicketDetails_ShouldReturnNotFound_WhenTicketDoesNotExist()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var admin = CreateUser("admin.controller.notfound@teste.com", UserRole.Admin);
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        var service = BuildService(context);
        var controller = BuildController(service, admin);

        var result = await controller.GetTicketDetails(Guid.NewGuid());
        Assert.IsType<NotFoundObjectResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin support tickets controller em memory integracao | Add mensagem | Deve retornar nao autorizado quando admin actor nao pode resolved.
    /// </summary>
    [Fact(DisplayName = "Admin support tickets controller em memory integracao | Add mensagem | Deve retornar nao autorizado quando admin actor nao pode resolved")]
    public async Task AddMessage_ShouldReturnUnauthorized_WhenAdminActorCannotBeResolved()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var provider = CreateUser("provider.controller.actor@teste.com", UserRole.Provider);
        context.Users.Add(provider);
        await context.SaveChangesAsync();

        var ticket = new SupportTicket
        {
            ProviderId = provider.Id,
            Subject = "Preciso ajuda",
            Category = "General",
            Priority = SupportTicketPriority.Medium,
            Status = SupportTicketStatus.Open,
            OpenedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            LastInteractionAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };
        ticket.AddMessage(provider.Id, UserRole.Provider, "Mensagem inicial.");
        context.SupportTickets.Add(ticket);
        await context.SaveChangesAsync();

        var service = BuildService(context);
        var staleAdminUser = CreateUser("admin.controller.stale@teste.com", UserRole.Admin);
        var controller = BuildController(service, staleAdminUser, Guid.NewGuid());

        var result = await controller.AddMessage(
            ticket.Id,
            new AdminSupportTicketMessageRequestDto("Tentativa de resposta"));

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var payload = unauthorized.Value?.ToString() ?? string.Empty;
        Assert.NotNull(unauthorized.Value);
    }

    private static IAdminSupportTicketService BuildService(ConsertaPraMimDbContext context)
    {
        return new AdminSupportTicketService(
            new SupportTicketRepository(context),
            new UserRepository(context),
            new AdminAuditLogRepository(context));
    }

    private static AdminSupportTicketsController BuildController(
        IAdminSupportTicketService service,
        User adminUser,
        Guid? claimsUserIdOverride = null)
    {
        var controller = new AdminSupportTicketsController(service, Mock.Of<IFileStorageService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, (claimsUserIdOverride ?? adminUser.Id).ToString()),
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
            Role = role
        };
    }
}
