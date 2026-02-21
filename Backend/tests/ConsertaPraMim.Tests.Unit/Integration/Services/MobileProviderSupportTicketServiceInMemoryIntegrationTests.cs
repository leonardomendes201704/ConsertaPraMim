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
    /// <summary>
    /// Cenario: prestador consulta seu historico de chamados e nao deve ver tickets de terceiros.
    /// Passos: cria chamados para dois prestadores diferentes e lista tickets filtrando pelo prestador A.
    /// Resultado esperado: retorno contem apenas tickets pertencentes ao prestador autenticado.
    /// </summary>
    [Fact(DisplayName = "Mobile prestador support ticket servico em memory integracao | Criar e listar support tickets | Deve retornar only owned tickets")]
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

    /// <summary>
    /// Cenario: seguranca de suporte mobile impede acesso cruzado entre prestadores.
    /// Passos: prestador A tenta detalhar, responder e fechar ticket criado pelo prestador B.
    /// Resultado esperado: todas as operacoes retornam erro de nao encontrado no escopo do prestador A.
    /// </summary>
    [Fact(DisplayName = "Mobile prestador support ticket servico em memory integracao | Prestador | Deve nao access ticket de another prestador")]
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

    /// <summary>
    /// Cenario: fluxo normal de atendimento pelo prestador inclui complementar mensagem e encerrar chamado.
    /// Passos: cria ticket, adiciona nova mensagem e executa fechamento pelo mesmo prestador dono do chamado.
    /// Resultado esperado: lifecycle evolui corretamente ate Closed e conversa preserva historico de mensagens.
    /// </summary>
    [Fact(DisplayName = "Mobile prestador support ticket servico em memory integracao | Add mensagem e fechar | Deve atualizar ticket lifecycle")]
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

    /// <summary>
    /// Cenario: falha no canal de notificacao nao pode bloquear abertura de chamado do prestador.
    /// Passos: injeta servico de notificacao que lanca excecao e executa CreateSupportTicketAsync.
    /// Resultado esperado: criacao do ticket permanece bem-sucedida mesmo com erro no envio de notificacoes.
    /// </summary>
    [Fact(DisplayName = "Mobile prestador support ticket servico em memory integracao | Criar support ticket | Deve succeed quando notificacao falha")]
    public async Task CreateSupportTicket_ShouldSucceed_WhenNotificationFails()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var provider = CreateUser("provider.notification.failure@teste.com", UserRole.Provider);
        var admin = CreateUser("admin.notification.failure@teste.com", UserRole.Admin);
        context.Users.AddRange(provider, admin);
        await context.SaveChangesAsync();

        var service = BuildService(
            context,
            userRepository: new UserRepository(context),
            notificationService: new ThrowingNotificationService());

        var result = await service.CreateSupportTicketAsync(provider.Id, new MobileProviderCreateSupportTicketRequestDto(
            "Falha de notificacao",
            "General",
            (int)SupportTicketPriority.Medium,
            "O fluxo principal deve continuar."));

        Assert.True(result.Success);
        Assert.NotNull(result.Ticket);
    }

    private static MobileProviderService BuildService(
        ConsertaPraMimDbContext context,
        IUserRepository? userRepository = null,
        INotificationService? notificationService = null)
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
            userRepository ?? Mock.Of<IUserRepository>(),
            Mock.Of<IServiceCategoryRepository>(),
            repository,
            notificationService);
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

    private sealed class ThrowingNotificationService : INotificationService
    {
        public Task SendNotificationAsync(string recipient, string subject, string message, string? actionUrl = null)
        {
            throw new InvalidOperationException("Falha simulada no canal de notificacao.");
        }
    }
}
