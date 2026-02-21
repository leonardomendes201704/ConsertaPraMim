using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Data;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;

namespace ConsertaPraMim.Tests.Unit.Integration.Services;

public class AdminSupportTicketServiceInMemoryIntegrationTests
{
    /// <summary>
    /// Cenario: existem chamados em estados diferentes e o painel administrativo precisa refletir fila e indicadores operacionais.
    /// Passos: o teste cria tickets abertos e aguardando prestador, incluindo atribuicao e mensagens para compor os contadores.
    /// Resultado esperado: a listagem paginada retorna itens e indicadores coerentes de aberto, aguardando, sem resposta inicial e sem atribuicao.
    /// </summary>
    [Fact(DisplayName = "Admin support ticket servico em memory integracao | Obter tickets | Deve retornar queue com indicators")]
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

    /// <summary>
    /// Cenario: o admin responde pela primeira vez um chamado aberto pelo prestador.
    /// Passos: o teste adiciona mensagem administrativa em ticket aberto com historico inicial do prestador.
    /// Resultado esperado: o ticket muda para WaitingProvider, registra FirstAdminResponseAtUtc e acumula as duas mensagens no thread.
    /// </summary>
    [Fact(DisplayName = "Admin support ticket servico em memory integracao | Add mensagem | Deve set waiting prestador e first resposta")]
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

    /// <summary>
    /// Cenario: a sessao do admin chega com userId desatualizado, mas o email ainda identifica corretamente o operador.
    /// Passos: o teste envia id inexistente no token e email de admin valido ao publicar resposta no ticket.
    /// Resultado esperado: o autor da mensagem eh resolvido pelo email e a resposta eh gravada como usuario admin correto.
    /// </summary>
    [Fact(DisplayName = "Admin support ticket servico em memory integracao | Add mensagem | Deve resolve admin por email quando token usuario id stale")]
    public async Task AddMessageAsync_ShouldResolveAdminByEmail_WhenTokenUserIdIsStale()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var provider = CreateUser("provider.staleid@teste.com", UserRole.Provider);
        var admin = CreateUser("admin.staleid@teste.com", UserRole.Admin);
        context.Users.AddRange(provider, admin);
        await context.SaveChangesAsync();

        var ticket = BuildTicket(provider.Id, "Erro tecnico", SupportTicketPriority.High, SupportTicketStatus.Open);
        ticket.AddMessage(provider.Id, UserRole.Provider, "Aplicativo travando.");
        context.SupportTickets.Add(ticket);
        await context.SaveChangesAsync();

        var service = BuildService(context);
        var staleAdminId = Guid.NewGuid();
        var result = await service.AddMessageAsync(
            ticket.Id,
            staleAdminId,
            admin.Email,
            new AdminSupportTicketMessageRequestDto("Resposta usando sessao antiga."));

        Assert.True(result.Success);
        Assert.NotNull(result.Message);
        Assert.Equal(admin.Id, result.Message!.AuthorUserId);
        Assert.Equal(UserRole.Admin.ToString(), result.Message.AuthorRole);
    }

    /// <summary>
    /// Cenario: nao ha admin correspondente nem por id nem por email ao tentar responder um chamado.
    /// Passos: o teste tenta publicar mensagem administrativa com credenciais inconsistentes para o ator.
    /// Resultado esperado: o fluxo falha com codigo de ator administrativo nao encontrado.
    /// </summary>
    [Fact(DisplayName = "Admin support ticket servico em memory integracao | Add mensagem | Deve falhar quando admin nao pode resolved")]
    public async Task AddMessageAsync_ShouldFail_WhenAdminCannotBeResolved()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var provider = CreateUser("provider.missingactor@teste.com", UserRole.Provider);
        context.Users.Add(provider);
        await context.SaveChangesAsync();

        var ticket = BuildTicket(provider.Id, "Erro tecnico", SupportTicketPriority.High, SupportTicketStatus.Open);
        ticket.AddMessage(provider.Id, UserRole.Provider, "Aplicativo travando.");
        context.SupportTickets.Add(ticket);
        await context.SaveChangesAsync();

        var service = BuildService(context);
        var result = await service.AddMessageAsync(
            ticket.Id,
            Guid.NewGuid(),
            "admin.inexistente@teste.com",
            new AdminSupportTicketMessageRequestDto("Tentativa invalida."));

        Assert.False(result.Success);
        Assert.Equal("admin_support_actor_not_found", result.ErrorCode);
    }

    /// <summary>
    /// Cenario: o chamado eh reatribuido para outro admin e em seguida encerrado.
    /// Passos: o teste executa atribuicao entre admins e atualizacao de status para Closed no mesmo ticket.
    /// Resultado esperado: a trilha de auditoria persiste eventos de mudanca de responsavel, mudanca de status e fechamento.
    /// </summary>
    [Fact(DisplayName = "Admin support ticket servico em memory integracao | Atualizar status e assign | Deve persistir audit trail")]
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

    /// <summary>
    /// Cenario: um chamado previamente encerrado precisa ser reaberto para novo tratamento.
    /// Passos: o teste tenta transicao de Closed para Open com justificativa administrativa.
    /// Resultado esperado: a reabertura eh permitida e os eventos de auditoria de alteracao e reabertura sao registrados.
    /// </summary>
    [Fact(DisplayName = "Admin support ticket servico em memory integracao | Atualizar status | Deve allow reopen de closed e record audit")]
    public async Task UpdateStatusAsync_ShouldAllowReopenFromClosed_AndRecordAudit()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var provider = CreateUser("provider.reopen@teste.com", UserRole.Provider);
        var admin = CreateUser("admin.reopen@teste.com", UserRole.Admin);
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

        Assert.True(result.Success);
        Assert.NotNull(result.Ticket);
        Assert.Equal(SupportTicketStatus.Open.ToString(), result.Ticket!.Ticket.Status);

        var auditActions = context.AdminAuditLogs
            .Where(x => x.TargetType == "SupportTicket" && x.TargetId == ticket.Id)
            .Select(x => x.Action)
            .ToList();

        Assert.Contains("support_ticket_status_changed", auditActions);
        Assert.Contains("support_ticket_reopened", auditActions);
    }

    /// <summary>
    /// Cenario: o admin tenta aplicar uma transicao de status nao permitida para ticket fechado.
    /// Passos: o teste solicita mudanca de Closed para WaitingProvider, que nao faz parte do fluxo valido.
    /// Resultado esperado: o servico bloqueia a operacao com erro de transicao invalida.
    /// </summary>
    [Fact(DisplayName = "Admin support ticket servico em memory integracao | Atualizar status | Deve block invalido transition de closed para waiting prestador")]
    public async Task UpdateStatusAsync_ShouldBlockInvalidTransitionFromClosed_ToWaitingProvider()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var provider = CreateUser("provider.closed.waiting@teste.com", UserRole.Provider);
        var admin = CreateUser("admin.closed.waiting@teste.com", UserRole.Admin);
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
            new AdminSupportTicketStatusUpdateRequestDto(SupportTicketStatus.WaitingProvider.ToString(), "Reabrindo"));

        Assert.False(result.Success);
        Assert.Equal("admin_support_invalid_transition", result.ErrorCode);
    }

    /// <summary>
    /// Cenario: o envio de notificacao externa falha no momento em que o admin responde o ticket.
    /// Passos: o teste injeta servico de notificacao com excecao e publica resposta administrativa.
    /// Resultado esperado: a resposta e o estado do ticket sao persistidos mesmo com falha no canal de notificacao.
    /// </summary>
    [Fact(DisplayName = "Admin support ticket servico em memory integracao | Add mensagem | Deve succeed quando notificacao falha")]
    public async Task AddMessageAsync_ShouldSucceed_WhenNotificationFails()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var provider = CreateUser("provider.notify.admin@teste.com", UserRole.Provider);
        var admin = CreateUser("admin.notify.admin@teste.com", UserRole.Admin);
        context.Users.AddRange(provider, admin);
        await context.SaveChangesAsync();

        var ticket = BuildTicket(provider.Id, "Chamado notificacao", SupportTicketPriority.High, SupportTicketStatus.Open);
        ticket.AddMessage(provider.Id, UserRole.Provider, "Mensagem inicial.");
        context.SupportTickets.Add(ticket);
        await context.SaveChangesAsync();

        var service = BuildService(context, new ThrowingNotificationService());
        var result = await service.AddMessageAsync(
            ticket.Id,
            admin.Id,
            admin.Email,
            new AdminSupportTicketMessageRequestDto("Resposta admin"));

        Assert.True(result.Success);
        Assert.NotNull(result.Ticket);
        Assert.Equal(SupportTicketStatus.WaitingProvider.ToString(), result.Ticket!.Ticket.Status);
    }

    /// <summary>
    /// Cenario: o admin tenta atribuir chamado para um usuario que nao possui papel administrativo.
    /// Passos: o teste cria alvo de atribuicao com role de prestador e executa o fluxo de assign.
    /// Resultado esperado: a atribuicao eh rejeitada com erro de destinatario nao admin.
    /// </summary>
    [Fact(DisplayName = "Admin support ticket servico em memory integracao | Assign | Deve reject non admin assignee")]
    public async Task AssignAsync_ShouldRejectNonAdminAssignee()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var provider = CreateUser("provider.assign.owner@teste.com", UserRole.Provider);
        var admin = CreateUser("admin.assign.owner@teste.com", UserRole.Admin);
        var nonAdminTarget = CreateUser("provider.assign.target@teste.com", UserRole.Provider);
        context.Users.AddRange(provider, admin, nonAdminTarget);
        await context.SaveChangesAsync();

        var ticket = BuildTicket(provider.Id, "Atribuicao invalida", SupportTicketPriority.Medium, SupportTicketStatus.Open);
        ticket.AddMessage(provider.Id, UserRole.Provider, "Preciso de ajuda.");
        context.SupportTickets.Add(ticket);
        await context.SaveChangesAsync();

        var service = BuildService(context);
        var result = await service.AssignAsync(
            ticket.Id,
            admin.Id,
            admin.Email,
            new AdminSupportTicketAssignRequestDto(nonAdminTarget.Id, "Tentativa invalida"));

        Assert.False(result.Success);
        Assert.Equal("admin_support_assignee_not_admin", result.ErrorCode);
    }

    private static AdminSupportTicketService BuildService(
        ConsertaPraMimDbContext context,
        INotificationService? notificationService = null)
    {
        return new AdminSupportTicketService(
            new SupportTicketRepository(context),
            new UserRepository(context),
            new AdminAuditLogRepository(context),
            notificationService);
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

    private sealed class ThrowingNotificationService : INotificationService
    {
        public Task SendNotificationAsync(string recipient, string subject, string message, string? actionUrl = null)
        {
            throw new InvalidOperationException("Falha simulada no canal de notificacao.");
        }
    }
}
