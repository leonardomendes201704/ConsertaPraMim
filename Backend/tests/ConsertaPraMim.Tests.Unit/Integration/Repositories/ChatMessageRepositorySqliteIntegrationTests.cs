using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;

namespace ConsertaPraMim.Tests.Unit.Integration.Repositories;

public class ChatMessageRepositorySqliteIntegrationTests
{
    /// <summary>
    /// Cenario: tela de conversa precisa montar historico completo entre cliente e prestador com anexos.
    /// Passos: persiste duas mensagens em ordem temporal (uma com anexo), depois consulta GetConversationAsync.
    /// Resultado esperado: retorno vem ordenado por criacao, com remetente carregado e anexo associado na mensagem correta.
    /// </summary>
    [Fact(DisplayName = "Chat mensagem repository sqlite integracao | Obter conversation | Deve retornar mensagens ordered com sender e anexos")]
    public async Task GetConversationAsync_ShouldReturnMessagesOrderedWithSenderAndAttachments()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var client = CreateClient("client.chat@teste.com");
            var provider = CreateProvider("provider.chat@teste.com");
            var request = CreateRequest(client.Id, "Consertar ventilador");

            var firstMessage = new ChatMessage
            {
                RequestId = request.Id,
                ProviderId = provider.Id,
                SenderId = client.Id,
                SenderRole = UserRole.Client,
                Text = "Oi, voce atende hoje?",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            };

            firstMessage.Attachments.Add(new ChatAttachment
            {
                FileUrl = "/uploads/chat/foto1.jpg",
                FileName = "foto1.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 2000,
                MediaKind = "image"
            });

            var secondMessage = new ChatMessage
            {
                RequestId = request.Id,
                ProviderId = provider.Id,
                SenderId = provider.Id,
                SenderRole = UserRole.Provider,
                Text = "Atendo sim.",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            };

            context.Users.AddRange(client, provider);
            context.ServiceRequests.Add(request);
            context.ChatMessages.AddRange(firstMessage, secondMessage);
            await context.SaveChangesAsync();

            var repository = new ChatMessageRepository(context);
            var result = await repository.GetConversationAsync(request.Id, provider.Id);

            Assert.Equal(2, result.Count);
            Assert.Equal(firstMessage.Id, result[0].Id);
            Assert.NotNull(result[0].Sender);
            Assert.Single(result[0].Attachments);
        }
    }

    /// <summary>
    /// Cenario: painel analitico consulta mensagens de chat somente dentro de uma janela de tempo.
    /// Passos: grava mensagem antiga e mensagem valida, executa GetByPeriodAsync com intervalo restrito de 1 hora.
    /// Resultado esperado: apenas mensagem dentro do range e retornada, com relacionamentos de solicitacao e cliente carregados.
    /// </summary>
    [Fact(DisplayName = "Chat mensagem repository sqlite integracao | Obter por period | Deve filter mensagens within date range")]
    public async Task GetByPeriodAsync_ShouldFilterMessagesWithinDateRange()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var client = CreateClient("client.period@teste.com");
            var provider = CreateProvider("provider.period@teste.com");
            var request = CreateRequest(client.Id, "Consertar tomada");

            var tooOld = new ChatMessage
            {
                RequestId = request.Id,
                ProviderId = provider.Id,
                SenderId = client.Id,
                SenderRole = UserRole.Client,
                Text = "Mensagem antiga",
                CreatedAt = DateTime.UtcNow.AddHours(-10)
            };

            var inRange = new ChatMessage
            {
                RequestId = request.Id,
                ProviderId = provider.Id,
                SenderId = provider.Id,
                SenderRole = UserRole.Provider,
                Text = "Mensagem valida",
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            };

            context.Users.AddRange(client, provider);
            context.ServiceRequests.Add(request);
            context.ChatMessages.AddRange(tooOld, inRange);
            await context.SaveChangesAsync();

            var repository = new ChatMessageRepository(context);
            var fromUtc = DateTime.UtcNow.AddHours(-1);
            var toUtc = DateTime.UtcNow;
            var result = await repository.GetByPeriodAsync(fromUtc, toUtc);

            Assert.Single(result);
            Assert.Equal(inRange.Id, result[0].Id);
            Assert.NotNull(result[0].Request);
            Assert.NotNull(result[0].Request.Client);
        }
    }

    /// <summary>
    /// Cenario: mecanismo de recibos precisa listar so mensagens do outro participante ainda nao confirmadas.
    /// Passos: cria mensagens de prestador pendente/lida e uma mensagem do proprio cliente, depois consulta pendencias.
    /// Resultado esperado: listas de delivered/unread contem somente a mensagem pendente enviada pelo outro lado da conversa.
    /// </summary>
    [Fact(DisplayName = "Chat mensagem repository sqlite integracao | Obter pending receipts | Deve retornar only mensagens de other participant")]
    public async Task GetPendingReceiptsAsync_ShouldReturnOnlyMessagesFromOtherParticipant()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var client = CreateClient("client.receipt@teste.com");
            var provider = CreateProvider("provider.receipt@teste.com");
            var request = CreateRequest(client.Id, "Trocar disjuntor");

            var fromProviderPending = new ChatMessage
            {
                RequestId = request.Id,
                ProviderId = provider.Id,
                SenderId = provider.Id,
                SenderRole = UserRole.Provider,
                Text = "Chego em 30 minutos.",
                CreatedAt = DateTime.UtcNow.AddMinutes(-4)
            };

            var fromProviderRead = new ChatMessage
            {
                RequestId = request.Id,
                ProviderId = provider.Id,
                SenderId = provider.Id,
                SenderRole = UserRole.Provider,
                Text = "Mensagem ja lida.",
                DeliveredAt = DateTime.UtcNow.AddMinutes(-3),
                ReadAt = DateTime.UtcNow.AddMinutes(-2),
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            };

            var fromClient = new ChatMessage
            {
                RequestId = request.Id,
                ProviderId = provider.Id,
                SenderId = client.Id,
                SenderRole = UserRole.Client,
                Text = "Ok, aguardando.",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            };

            context.Users.AddRange(client, provider);
            context.ServiceRequests.Add(request);
            context.ChatMessages.AddRange(fromProviderPending, fromProviderRead, fromClient);
            await context.SaveChangesAsync();

            var repository = new ChatMessageRepository(context);

            var deliveredPending = await repository.GetPendingReceiptsAsync(request.Id, provider.Id, client.Id, false);
            var unreadPending = await repository.GetPendingReceiptsAsync(request.Id, provider.Id, client.Id, true);

            Assert.Single(deliveredPending);
            Assert.Equal(fromProviderPending.Id, deliveredPending[0].Id);
            Assert.Single(unreadPending);
            Assert.Equal(fromProviderPending.Id, unreadPending[0].Id);
        }
    }

    /// <summary>
    /// Cenario: atualizacao em lote de recibos deve persistir carimbos de entrega e leitura sem perder a mensagem original.
    /// Passos: salva mensagem, define DeliveredAt/ReadAt em memoria e chama UpdateRangeAsync para gravacao em banco.
    /// Resultado esperado: registro persistido passa a conter ambos os timestamps de recibo apos recarga da entidade.
    /// </summary>
    [Fact(DisplayName = "Chat mensagem repository sqlite integracao | Atualizar range | Deve persistir entregue e lido timestamps")]
    public async Task UpdateRangeAsync_ShouldPersistDeliveredAndReadTimestamps()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var client = CreateClient("client.update.receipt@teste.com");
            var provider = CreateProvider("provider.update.receipt@teste.com");
            var request = CreateRequest(client.Id, "Consertar chuveiro");

            var message = new ChatMessage
            {
                RequestId = request.Id,
                ProviderId = provider.Id,
                SenderId = provider.Id,
                SenderRole = UserRole.Provider,
                Text = "Mensagem para atualizar"
            };

            context.Users.AddRange(client, provider);
            context.ServiceRequests.Add(request);
            context.ChatMessages.Add(message);
            await context.SaveChangesAsync();

            var repository = new ChatMessageRepository(context);
            message.DeliveredAt = DateTime.UtcNow.AddMinutes(-2);
            message.ReadAt = DateTime.UtcNow.AddMinutes(-1);
            await repository.UpdateRangeAsync(new[] { message });

            var persisted = await context.ChatMessages.FindAsync(message.Id);
            Assert.NotNull(persisted);
            Assert.NotNull(persisted!.DeliveredAt);
            Assert.NotNull(persisted.ReadAt);
        }
    }

    private static User CreateClient(string email)
    {
        return new User
        {
            Name = "Cliente",
            Email = email,
            PasswordHash = "hash",
            Phone = "11999999999",
            Role = UserRole.Client
        };
    }

    private static User CreateProvider(string email)
    {
        return new User
        {
            Name = "Prestador",
            Email = email,
            PasswordHash = "hash",
            Phone = "11888888888",
            Role = UserRole.Provider
        };
    }

    private static ServiceRequest CreateRequest(Guid clientId, string description)
    {
        return new ServiceRequest
        {
            ClientId = clientId,
            Category = ServiceCategory.Electrical,
            Status = ServiceRequestStatus.Created,
            Description = description,
            AddressStreet = "Rua Teste",
            AddressCity = "Praia Grande",
            AddressZip = "11704150",
            Latitude = -23.96,
            Longitude = -46.32
        };
    }
}
