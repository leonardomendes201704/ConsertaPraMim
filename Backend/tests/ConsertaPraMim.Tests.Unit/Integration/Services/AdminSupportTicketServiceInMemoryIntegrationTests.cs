using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Data;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;

namespace ConsertaPraMim.Tests.Unit.Integration.Services;

public class AdminSupportTicketServiceInMemoryIntegrationTests
{
    [Fact]
    public async Task GetTicketsAsync_ShouldReturnQueueWithIndicators()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var provider = CreateUser("provider.queue@teste.com", UserRole.Provider);
        var admin = CreateUser("admin.queue@teste.com", UserRole.Admin);
        context.Users.AddRange(provider, admin);
        await context.SaveChangesAsync();

        var openTicket = BuildTicket(provider.Id, "Pagamento", SupportTicketPriority.High, SupportTicketStatus.Open);
        openTicket.AddMessage(provider.Id, UserRole.Provider, "Ticket aberto.");

        var waitingTicket = BuildTicket(provider.Id, "Agenda", SupportTicketPriority.Medium, SupportTicketStatus.WaitingProvider);
        waitingTicket.AssignAdmin(admin.Id);
        waitingTicket.AddMessage(provider.Id, UserRole.Provider, "Mensagem inicial.");
        waitingTicket.AddMessage(admin.Id, UserRole.Admin, "Retorno admin.");

        context.SupportTickets.AddRange(openTicket, waitingTicket);
        await context.SaveChangesAsync();

        var service = BuildService(context);
        var response = await service.GetTicketsAsync(new AdminSupportTicketListQueryDto(Page: 1, PageSize: 20));

        Assert.Equal(2, response.TotalCount);
        Assert.Equal(2, response.Items.Count);
        Assert.Equal(1, response.Indicators.OpenCount);
        Assert.Equal(1, response.Indicators.WaitingProviderCount);
        Assert.Equal(1, response.Indicators.WithoutFirstAdminResponseCount);
        Assert.Equal(1, response.Indicators.UnassignedCount);
    }

    [Fact]
    public async Task AddMessageAsync_ShouldSetWaitingProviderAndFirstResponse()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var provider = CreateUser("provider.reply@teste.com", UserRole.Provider);
        var admin = CreateUser("admin.reply@teste.com", UserRole.Admin);
        context.Users.AddRange(provider, admin);
        await context.SaveChangesAsync();

        var ticket = BuildTicket(provider.Id, "Erro tecnico", SupportTicketPriority.Critical, SupportTicketStatus.Open);
        ticket.AddMessage(provider.Id, UserRole.Provider, "Aplicativo travando.");
        context.SupportTickets.Add(ticket);
        await context.SaveChangesAsync();

        var service = BuildService(context);
        var result = await service.AddMessageAsync(
            ticket.Id,
            admin.Id,
            admin.Email,
            new AdminSupportTicketMessageRequestDto("Vamos analisar agora."));

        Assert.True(result.Success);
        Assert.NotNull(result.Ticket);
        Assert.Equal(SupportTicketStatus.WaitingProvider.ToString(), result.Ticket!.Ticket.Status);
        Assert.NotNull(result.Ticket.Ticket.FirstAdminResponseAtUtc);
        Assert.Equal(2, result.Ticket.Messages.Count);
    }

    [Fact]
    public async Task UpdateStatusAndAssign_ShouldPersistAuditTrail()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var provider = CreateUser("provider.audit@teste.com", UserRole.Provider);
        var adminA = CreateUser("admin.audit.a@teste.com", UserRole.Admin);
        var adminB = CreateUser("admin.audit.b@teste.com", UserRole.Admin);
        context.Users.AddRange(provider, adminA, adminB);
        await context.SaveChangesAsync();

        var ticket = BuildTicket(provider.Id, "SLA", SupportTicketPriority.Medium, SupportTicketStatus.InProgress);
        ticket.AddMessage(provider.Id, UserRole.Provider, "Aguardando retorno.");
        context.SupportTickets.Add(ticket);
        await context.SaveChangesAsync();

        var service = BuildService(context);
        var assign = await service.AssignAsync(
            ticket.Id,
            adminA.Id,
            adminA.Email,
            new AdminSupportTicketAssignRequestDto(adminB.Id, "Escalando para plantonista."));
        var close = await service.UpdateStatusAsync(
            ticket.Id,
            adminB.Id,
            adminB.Email,
            new AdminSupportTicketStatusUpdateRequestDto(SupportTicketStatus.Closed.ToString(), "Resolvido."));

        Assert.True(assign.Success);
        Assert.True(close.Success);

        var auditActions = context.AdminAuditLogs
            .Where(x => x.TargetType == "SupportTicket" && x.TargetId == ticket.Id)
            .Select(x => x.Action)
            .ToList();

        Assert.Contains("support_ticket_assignment_changed", auditActions);
        Assert.Contains("support_ticket_status_changed", auditActions);
        Assert.Contains("support_ticket_closed", auditActions);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldBlockInvalidTransitionFromClosed()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var provider = CreateUser("provider.closed@teste.com", UserRole.Provider);
        var admin = CreateUser("admin.closed@teste.com", UserRole.Admin);
        context.Users.AddRange(provider, admin);
        await context.SaveChangesAsync();

        var ticket = BuildTicket(provider.Id, "Chamado finalizado", SupportTicketPriority.Low, SupportTicketStatus.Closed);
        ticket.AddMessage(provider.Id, UserRole.Provider, "Finalizado.");
        context.SupportTickets.Add(ticket);
        await context.SaveChangesAsync();

        var service = BuildService(context);
        var result = await service.UpdateStatusAsync(
            ticket.Id,
            admin.Id,
            admin.Email,
            new AdminSupportTicketStatusUpdateRequestDto(SupportTicketStatus.Open.ToString(), "Reabrindo"));

        Assert.False(result.Success);
        Assert.Equal("admin_support_invalid_transition", result.ErrorCode);
    }

    private static AdminSupportTicketService BuildService(ConsertaPraMimDbContext context)
    {
        return new AdminSupportTicketService(
            new SupportTicketRepository(context),
            new UserRepository(context),
            new AdminAuditLogRepository(context));
    }

    private static SupportTicket BuildTicket(
        Guid providerId,
        string subject,
        SupportTicketPriority priority,
        SupportTicketStatus status)
    {
        var now = DateTime.UtcNow;
        return new SupportTicket
        {
            ProviderId = providerId,
            Subject = subject,
            Category = "General",
            Priority = priority,
            Status = status,
            OpenedAtUtc = now.AddMinutes(-15),
            LastInteractionAtUtc = now.AddMinutes(-5)
        };
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
