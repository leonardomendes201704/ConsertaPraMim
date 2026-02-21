using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Tests.Unit.Integration.Repositories;

public class SupportTicketPersistenceInMemoryIntegrationTests
{
    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Support ticket persistence em memory integracao | Support ticket com mensagens | Deve persistir e load com relations.
    /// </summary>
    [Fact(DisplayName = "Support ticket persistence em memory integracao | Support ticket com mensagens | Deve persistir e load com relations")]
    public async Task SupportTicket_WithMessages_ShouldPersistAndLoadWithRelations()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        {
            var provider = CreateProvider("provider.support.ticket@teste.com");
            var admin = CreateAdmin("admin.support.ticket@teste.com");
            context.Users.AddRange(provider, admin);
            await context.SaveChangesAsync();

            var ticket = new SupportTicket
            {
                ProviderId = provider.Id,
                Subject = "Duvida sobre comissao",
                Category = "Billing",
                Priority = SupportTicketPriority.High,
                Status = SupportTicketStatus.Open
            };

            ticket.AssignAdmin(admin.Id);
            ticket.AddMessage(
                authorUserId: provider.Id,
                authorRole: UserRole.Provider,
                messageText: "Preciso de ajuda com cobranca.");
            ticket.AddMessage(
                authorUserId: admin.Id,
                authorRole: UserRole.Admin,
                messageText: "Vamos verificar e retorno hoje.");
            ticket.ChangeStatus(SupportTicketStatus.InProgress);

            context.SupportTickets.Add(ticket);
            await context.SaveChangesAsync();

            var persisted = await context.SupportTickets
                .Include(t => t.Provider)
                .Include(t => t.AssignedAdminUser)
                .Include(t => t.Messages)
                .SingleAsync(t => t.Id == ticket.Id);

            Assert.Equal(provider.Id, persisted.ProviderId);
            Assert.Equal(admin.Id, persisted.AssignedAdminUserId);
            Assert.Equal(SupportTicketStatus.InProgress, persisted.Status);
            Assert.NotNull(persisted.FirstAdminResponseAtUtc);
            Assert.Equal(2, persisted.Messages.Count);
            Assert.All(persisted.Messages, m => Assert.Equal(ticket.Id, m.SupportTicketId));
        }
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Support ticket persistence em memory integracao | Deleting support ticket | Deve cascade excluir mensagens.
    /// </summary>
    [Fact(DisplayName = "Support ticket persistence em memory integracao | Deleting support ticket | Deve cascade excluir mensagens")]
    public async Task DeletingSupportTicket_ShouldCascadeDeleteMessages()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        {
            var provider = CreateProvider("provider.support.cascade@teste.com");
            context.Users.Add(provider);
            await context.SaveChangesAsync();

            var ticket = new SupportTicket
            {
                ProviderId = provider.Id,
                Subject = "Teste cascade",
                Category = "General",
                Priority = SupportTicketPriority.Medium
            };

            ticket.AddMessage(
                authorUserId: provider.Id,
                authorRole: UserRole.Provider,
                messageText: "Mensagem 1");
            ticket.AddMessage(
                authorUserId: provider.Id,
                authorRole: UserRole.Provider,
                messageText: "Mensagem 2");

            context.SupportTickets.Add(ticket);
            await context.SaveChangesAsync();

            var messageIds = await context.SupportTicketMessages
                .Where(m => m.SupportTicketId == ticket.Id)
                .Select(m => m.Id)
                .ToListAsync();

            Assert.Equal(2, messageIds.Count);

            context.SupportTickets.Remove(ticket);
            await context.SaveChangesAsync();

            var remainingMessages = await context.SupportTicketMessages
                .CountAsync(m => m.SupportTicketId == ticket.Id);

            Assert.Equal(0, remainingMessages);
        }
    }

    private static User CreateProvider(string email)
    {
        return new User
        {
            Name = "Prestador Suporte",
            Email = email,
            PasswordHash = "hash",
            Phone = "11999998888",
            Role = UserRole.Provider
        };
    }

    private static User CreateAdmin(string email)
    {
        return new User
        {
            Name = "Admin Suporte",
            Email = email,
            PasswordHash = "hash",
            Phone = "11911112222",
            Role = UserRole.Admin
        };
    }
}
