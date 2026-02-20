using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integration.Services;

public class MobileProviderSupportTicketServiceInMemoryIntegrationTests
{
    [Fact]
    public async Task CreateAndListSupportTickets_ShouldReturnOnlyOwnedTickets()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var providerA = CreateUser("provider.a.support@teste.com", UserRole.Provider);
        var providerB = CreateUser("provider.b.support@teste.com", UserRole.Provider);
        context.Users.AddRange(providerA, providerB);
        await context.SaveChangesAsync();

        var service = BuildService(context);

        await service.CreateSupportTicketAsync(providerA.Id, new MobileProviderCreateSupportTicketRequestDto(
            "Ajuda no faturamento",
            "Billing",
            (int)SupportTicketPriority.High,
            "Preciso revisar cobranca da ultima semana."));

        await service.CreateSupportTicketAsync(providerB.Id, new MobileProviderCreateSupportTicketRequestDto(
            "Duvida sobre SLA",
            "General",
            (int)SupportTicketPriority.Medium,
            "Qual o prazo de atendimento?"));

        await service.CreateSupportTicketAsync(providerA.Id, new MobileProviderCreateSupportTicketRequestDto(
            "Erro no app",
            "Technical",
            (int)SupportTicketPriority.Critical,
            "App fechando ao abrir pedidos."));

        var list = await service.GetSupportTicketsAsync(
            providerA.Id,
            new MobileProviderSupportTicketListQueryDto(Page: 1, PageSize: 10));

        Assert.Equal(2, list.TotalCount);
        Assert.Equal(2, list.Items.Count);
        Assert.All(list.Items, item => Assert.DoesNotContain("SLA", item.Subject, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Provider_ShouldNotAccessTicketFromAnotherProvider()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var providerA = CreateUser("provider.a.access@teste.com", UserRole.Provider);
        var providerB = CreateUser("provider.b.access@teste.com", UserRole.Provider);
        context.Users.AddRange(providerA, providerB);
        await context.SaveChangesAsync();

        var service = BuildService(context);
        var created = await service.CreateSupportTicketAsync(providerB.Id, new MobileProviderCreateSupportTicketRequestDto(
            "Ticket privado",
            "General",
            (int)SupportTicketPriority.Low,
            "Nao deveria aparecer para outro prestador."));

        Assert.True(created.Success);
        var foreignTicketId = created.Ticket!.Ticket.Id;

        var detail = await service.GetSupportTicketDetailsAsync(providerA.Id, foreignTicketId);
        var message = await service.AddSupportTicketMessageAsync(
            providerA.Id,
            foreignTicketId,
            new MobileProviderSupportTicketMessageRequestDto("Tentativa indevida"));
        var close = await service.CloseSupportTicketAsync(providerA.Id, foreignTicketId);

        Assert.False(detail.Success);
        Assert.Equal("mobile_provider_support_ticket_not_found", detail.ErrorCode);
        Assert.False(message.Success);
        Assert.Equal("mobile_provider_support_ticket_not_found", message.ErrorCode);
        Assert.False(close.Success);
        Assert.Equal("mobile_provider_support_ticket_not_found", close.ErrorCode);
    }

    [Fact]
    public async Task AddMessageAndClose_ShouldUpdateTicketLifecycle()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var provider = CreateUser("provider.lifecycle@teste.com", UserRole.Provider);
        context.Users.Add(provider);
        await context.SaveChangesAsync();

        var service = BuildService(context);
        var created = await service.CreateSupportTicketAsync(provider.Id, new MobileProviderCreateSupportTicketRequestDto(
            "Solicitacao de suporte",
            "General",
            (int)SupportTicketPriority.Medium,
            "Mensagem inicial"));

        Assert.True(created.Success);
        var ticketId = created.Ticket!.Ticket.Id;

        var replied = await service.AddSupportTicketMessageAsync(
            provider.Id,
            ticketId,
            new MobileProviderSupportTicketMessageRequestDto("Complementando informacoes."));
        Assert.True(replied.Success);
        Assert.NotNull(replied.Message);

        var closed = await service.CloseSupportTicketAsync(provider.Id, ticketId);
        Assert.True(closed.Success);
        Assert.NotNull(closed.Ticket);
        Assert.Equal(SupportTicketStatus.Closed.ToString(), closed.Ticket!.Ticket.Status);
        Assert.True(closed.Ticket.Messages.Count >= 3);
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
