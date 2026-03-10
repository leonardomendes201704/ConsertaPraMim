using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class TelegramChatbotConversationServiceTests
{
    private readonly Mock<IChatbotConversationRepository> _chatbotConversationRepositoryMock;
    private readonly TelegramChatbotConversationService _service;

    public TelegramChatbotConversationServiceTests()
    {
        _chatbotConversationRepositoryMock = new Mock<IChatbotConversationRepository>();
        _service = new TelegramChatbotConversationService(_chatbotConversationRepositoryMock.Object);
    }

    /// <summary>
    /// Cenario: cliente inicia conversa nova no Telegram.
    /// Passos: repositorio nao encontra conversa existente, servico cria sessao com data local informada.
    /// Resultado esperado: conversa persistida com timestamps em UTC e channel conversation id vinculado ao cliente.
    /// </summary>
    [Fact(DisplayName = "Telegram chatbot servico | Open session | Deve criar conversa com datas em UTC")]
    public async Task OpenOrResumeConversationAsync_ShouldCreateConversationWithUtcDates()
    {
        var clientId = Guid.NewGuid();
        var interactionLocal = DateTime.SpecifyKind(new DateTime(2026, 3, 3, 10, 0, 0), DateTimeKind.Local);
        ChatbotConversation? persistedConversation = null;

        _chatbotConversationRepositoryMock
            .Setup(r => r.GetByClientAndChannelAsync(clientId, "telegram", "chat-1001"))
            .ReturnsAsync((ChatbotConversation?)null);
        _chatbotConversationRepositoryMock
            .Setup(r => r.AddConversationAsync(It.IsAny<ChatbotConversation>()))
            .Callback<ChatbotConversation>(conversation => persistedConversation = conversation)
            .Returns(Task.CompletedTask);

        var response = await _service.OpenOrResumeConversationAsync(
            new TelegramChatbotOpenConversationRequestDto(
                ClientId: clientId,
                Channel: "telegram",
                ChannelConversationId: "chat-1001",
                Status: ChatbotConversationStatus.Active,
                LastIntent: "triage_problem",
                LastStep: "collect_issue",
                MetadataJson: "{\"source\":\"telegram\"}",
                InteractionAtUtc: interactionLocal));

        Assert.NotNull(persistedConversation);
        var expectedUtc = interactionLocal.ToUniversalTime();
        Assert.Equal(expectedUtc, persistedConversation!.StartedAtUtc);
        Assert.Equal(expectedUtc, persistedConversation.LastInteractionAtUtc);
        Assert.Equal(expectedUtc, response.StartedAtUtc);
        Assert.Equal(DateTimeKind.Utc, response.StartedAtUtc.Kind);
        Assert.Equal(clientId, response.ClientId);
        Assert.Equal("chat-1001", response.ChannelConversationId);
    }

    /// <summary>
    /// Cenario: cliente tenta registrar mensagem em conversa pertencente a outro cliente.
    /// Passos: repositorio retorna conversa com clientId diferente do usuario autenticado.
    /// Resultado esperado: servico devolve null e nao persiste nova mensagem.
    /// </summary>
    [Fact(DisplayName = "Telegram chatbot servico | Register message | Deve bloquear acesso cruzado entre clientes")]
    public async Task RegisterMessageAsync_ShouldReturnNull_WhenConversationBelongsToAnotherClient()
    {
        var authenticatedClientId = Guid.NewGuid();
        var otherClientId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        _chatbotConversationRepositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(conversationId))
            .ReturnsAsync(new ChatbotConversation
            {
                Id = conversationId,
                ClientId = otherClientId,
                Channel = "telegram",
                ChannelConversationId = "chat-foreign",
                Status = ChatbotConversationStatus.Active
            });

        var result = await _service.RegisterMessageAsync(
            new TelegramChatbotRegisterMessageRequestDto(
                ConversationId: conversationId,
                ClientId: authenticatedClientId,
                Direction: ChatbotMessageDirection.Incoming,
                Source: "telegram",
                Content: "Mensagem de teste"));

        Assert.Null(result);
        _chatbotConversationRepositoryMock.Verify(
            r => r.AddMessageAsync(It.IsAny<ChatbotMessage>()),
            Times.Never);
    }

    /// <summary>
    /// Cenario: payload de mensagem com token negativo enviado pelo orquestrador.
    /// Passos: conversa valida encontrada e request com PromptTokens menor que zero.
    /// Resultado esperado: servico dispara InvalidOperationException para proteger persistencia.
    /// </summary>
    [Fact(DisplayName = "Telegram chatbot servico | Register message | Deve falhar com token negativo")]
    public async Task RegisterMessageAsync_ShouldThrowInvalidOperationException_WhenTokenIsNegative()
    {
        var clientId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        _chatbotConversationRepositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(conversationId))
            .ReturnsAsync(new ChatbotConversation
            {
                Id = conversationId,
                ClientId = clientId,
                Channel = "telegram",
                ChannelConversationId = "chat-2002",
                Status = ChatbotConversationStatus.Active
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RegisterMessageAsync(
                new TelegramChatbotRegisterMessageRequestDto(
                    ConversationId: conversationId,
                    ClientId: clientId,
                    Direction: ChatbotMessageDirection.Outgoing,
                    Source: "openai",
                    Content: "Resposta gerada",
                    PromptTokens: -1)));
    }

    /// <summary>
    /// Cenario: consulta de historico em conversa com datas sem kind definido retornadas pelo provider de banco.
    /// Passos: repositorio retorna entidade com DateTime Unspecified e servico monta DTO de historico.
    /// Resultado esperado: campos de data do retorno sao normalizados para Kind UTC.
    /// </summary>
    [Fact(DisplayName = "Telegram chatbot servico | Get history | Deve normalizar datas para UTC no retorno")]
    public async Task GetConversationHistoryAsync_ShouldNormalizeReturnedDatesToUtc()
    {
        var clientId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var startedUnspecified = new DateTime(2026, 3, 2, 14, 30, 0, DateTimeKind.Unspecified);
        var sentUnspecified = new DateTime(2026, 3, 2, 14, 45, 0, DateTimeKind.Unspecified);

        _chatbotConversationRepositoryMock
            .Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(new ChatbotConversation
            {
                Id = conversationId,
                ClientId = clientId,
                Channel = "telegram",
                ChannelConversationId = "chat-3003",
                Status = ChatbotConversationStatus.Active,
                StartedAtUtc = startedUnspecified,
                LastInteractionAtUtc = sentUnspecified
            });
        _chatbotConversationRepositoryMock
            .Setup(r => r.GetMessagesAsync(conversationId, 50))
            .ReturnsAsync(new List<ChatbotMessage>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    ClientId = clientId,
                    Direction = ChatbotMessageDirection.Incoming,
                    Source = "telegram",
                    Content = "Meu ar condicionado esta com erro CH26.",
                    SentAtUtc = sentUnspecified
                }
            });
        _chatbotConversationRepositoryMock
            .Setup(r => r.GetContextSnapshotsAsync(conversationId, 20))
            .ReturnsAsync(Array.Empty<ChatbotContextSnapshot>());
        _chatbotConversationRepositoryMock
            .Setup(r => r.GetActionLogsAsync(conversationId, 20))
            .ReturnsAsync(Array.Empty<ChatbotActionLog>());

        var history = await _service.GetConversationHistoryAsync(conversationId, clientId);

        Assert.NotNull(history);
        Assert.Equal(DateTimeKind.Utc, history!.Conversation.StartedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, history.Conversation.LastInteractionAtUtc.Kind);
        Assert.Single(history.Messages);
        Assert.Equal(DateTimeKind.Utc, history.Messages[0].SentAtUtc.Kind);
    }
}
