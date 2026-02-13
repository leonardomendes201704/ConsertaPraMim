using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ChatServiceTests
{
    private readonly Mock<IChatMessageRepository> _chatRepositoryMock;
    private readonly Mock<IServiceRequestRepository> _requestRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly ChatService _service;

    public ChatServiceTests()
    {
        _chatRepositoryMock = new Mock<IChatMessageRepository>();
        _requestRepositoryMock = new Mock<IServiceRequestRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _service = new ChatService(
            _chatRepositoryMock.Object,
            _requestRepositoryMock.Object,
            _userRepositoryMock.Object);
    }

    [Fact]
    public async Task CanAccessConversationAsync_ShouldReturnFalse_WhenRequestDoesNotExist()
    {
        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((ServiceRequest?)null);

        var result = await _service.CanAccessConversationAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Client");

        Assert.False(result);
    }

    [Fact]
    public async Task CanAccessConversationAsync_ShouldReturnTrue_ForRequestOwnerClient()
    {
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(CreateRequest(requestId, clientId, providerId));

        var result = await _service.CanAccessConversationAsync(
            requestId,
            providerId,
            clientId,
            "Client");

        Assert.True(result);
    }

    [Fact]
    public async Task GetConversationHistoryAsync_ShouldReturnEmpty_WhenUserCannotAccessConversation()
    {
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = clientId,
                Proposals = new List<Proposal>()
            });

        var history = await _service.GetConversationHistoryAsync(
            requestId,
            providerId,
            clientId,
            "Client");

        Assert.Empty(history);
        _chatRepositoryMock.Verify(
            r => r.GetConversationAsync(It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveRecipientIdAsync_ShouldResolveCounterpartUserInConversation()
    {
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(CreateRequest(requestId, clientId, providerId));

        var recipientForProvider = await _service.ResolveRecipientIdAsync(requestId, providerId, providerId);
        var recipientForClient = await _service.ResolveRecipientIdAsync(requestId, providerId, clientId);
        var recipientForOtherUser = await _service.ResolveRecipientIdAsync(requestId, providerId, Guid.NewGuid());

        Assert.Equal(clientId, recipientForProvider);
        Assert.Equal(providerId, recipientForClient);
        Assert.Null(recipientForOtherUser);
    }

    [Fact]
    public async Task SendMessageAsync_ShouldReturnNull_WhenTextAndAttachmentsAreInvalid()
    {
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(CreateRequest(requestId, clientId, providerId));

        var result = await _service.SendMessageAsync(
            requestId,
            providerId,
            clientId,
            "Client",
            "   ",
            new[]
            {
                new ChatAttachmentInputDto("javascript:alert(1)", "x.js", "application/javascript", 10),
                new ChatAttachmentInputDto("/uploads/private/file.jpg", "file.jpg", "image/jpeg", 10)
            });

        Assert.Null(result);
        _userRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
        _chatRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ChatMessage>()), Times.Never);
    }

    [Fact]
    public async Task SendMessageAsync_ShouldPersistTrimmedTextAndNormalizedAttachments_WhenPayloadIsValid()
    {
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var sender = new User
        {
            Id = providerId,
            Name = "Prestador Teste",
            Role = UserRole.Provider
        };

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(CreateRequest(requestId, clientId, providerId));
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(providerId))
            .ReturnsAsync(sender);

        ChatMessage? persistedMessage = null;
        _chatRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ChatMessage>()))
            .Callback<ChatMessage>(m => persistedMessage = m)
            .Returns(Task.CompletedTask);

        var result = await _service.SendMessageAsync(
            requestId,
            providerId,
            providerId,
            "Provider",
            "  Oi, consigo te atender hoje.  ",
            new[]
            {
                new ChatAttachmentInputDto("/uploads/chat/foto.jpg", "foto.jpg", "image/jpeg", 1500),
                new ChatAttachmentInputDto("https://localhost:7281/uploads/chat/video.mp4", "video.mp4", "video/mp4", 9000),
                new ChatAttachmentInputDto("ftp://localhost/uploads/chat/invalid.mp4", "invalid.mp4", "video/mp4", 10),
                new ChatAttachmentInputDto("https://localhost:7281/private/invalid.png", "invalid.png", "image/png", 10)
            });

        Assert.NotNull(result);
        Assert.Equal("Oi, consigo te atender hoje.", result!.Text);
        Assert.Equal("Prestador Teste", result.SenderName);
        Assert.Equal("Provider", result.SenderRole);
        Assert.Equal(2, result.Attachments.Count);

        Assert.NotNull(persistedMessage);
        Assert.Equal("Oi, consigo te atender hoje.", persistedMessage!.Text);
        Assert.Equal(2, persistedMessage.Attachments.Count);
        Assert.Contains(persistedMessage.Attachments, a => a.FileUrl == "/uploads/chat/foto.jpg" && a.MediaKind == "image");
        Assert.Contains(persistedMessage.Attachments, a => a.FileUrl == "https://localhost:7281/uploads/chat/video.mp4" && a.MediaKind == "video");
    }

    [Fact]
    public async Task MarkConversationDeliveredAsync_ShouldMarkPendingMessagesAsDelivered()
    {
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var pendingMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            ProviderId = providerId,
            SenderId = providerId,
            SenderRole = UserRole.Provider,
            Text = "Posso te atender hoje."
        };

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(CreateRequest(requestId, clientId, providerId));
        _chatRepositoryMock
            .Setup(r => r.GetPendingReceiptsAsync(requestId, providerId, clientId, false))
            .ReturnsAsync(new List<ChatMessage> { pendingMessage });

        IEnumerable<ChatMessage>? updated = null;
        _chatRepositoryMock
            .Setup(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<ChatMessage>>()))
            .Callback<IEnumerable<ChatMessage>>(items => updated = items)
            .Returns(Task.CompletedTask);

        var result = await _service.MarkConversationDeliveredAsync(requestId, providerId, clientId, "Client");

        Assert.Single(result);
        Assert.NotNull(result[0].DeliveredAt);
        Assert.Null(result[0].ReadAt);
        Assert.NotNull(updated);
        Assert.Single(updated!);
        _chatRepositoryMock.Verify(r => r.GetPendingReceiptsAsync(requestId, providerId, clientId, false), Times.Once);
        _chatRepositoryMock.Verify(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<ChatMessage>>()), Times.Once);
    }

    [Fact]
    public async Task MarkConversationReadAsync_ShouldMarkPendingMessagesAsReadAndDelivered()
    {
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var pendingMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            ProviderId = providerId,
            SenderId = providerId,
            SenderRole = UserRole.Provider,
            Text = "Tudo certo, confirmado."
        };

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(CreateRequest(requestId, clientId, providerId));
        _chatRepositoryMock
            .Setup(r => r.GetPendingReceiptsAsync(requestId, providerId, clientId, true))
            .ReturnsAsync(new List<ChatMessage> { pendingMessage });
        _chatRepositoryMock
            .Setup(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<ChatMessage>>()))
            .Returns(Task.CompletedTask);

        var result = await _service.MarkConversationReadAsync(requestId, providerId, clientId, "Client");

        Assert.Single(result);
        Assert.NotNull(result[0].ReadAt);
        Assert.NotNull(result[0].DeliveredAt);
        _chatRepositoryMock.Verify(r => r.GetPendingReceiptsAsync(requestId, providerId, clientId, true), Times.Once);
        _chatRepositoryMock.Verify(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<ChatMessage>>()), Times.Once);
    }

    [Fact]
    public async Task MarkConversationReadAsync_ShouldReturnEmpty_WhenUserCannotAccessConversation()
    {
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var nonParticipantUserId = Guid.NewGuid();

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = Guid.NewGuid(),
                Proposals = new List<Proposal>
                {
                    new() { ProviderId = providerId }
                }
            });

        var result = await _service.MarkConversationReadAsync(requestId, providerId, nonParticipantUserId, "Client");

        Assert.Empty(result);
        _chatRepositoryMock.Verify(
            r => r.GetPendingReceiptsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>()),
            Times.Never);
        _chatRepositoryMock.Verify(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<ChatMessage>>()), Times.Never);
    }

    [Fact]
    public async Task GetActiveConversationsAsync_ShouldGroupByConversationAndSortByLastMessage()
    {
        var now = DateTime.UtcNow;
        var clientId = Guid.NewGuid();
        var providerAId = Guid.NewGuid();
        var providerBId = Guid.NewGuid();
        var requestAId = Guid.NewGuid();
        var requestBId = Guid.NewGuid();

        var requestA = new ServiceRequest
        {
            Id = requestAId,
            ClientId = clientId,
            Client = new User { Id = clientId, Name = "Cliente" },
            Description = "Troca de tomada da cozinha",
            Proposals = new List<Proposal>
            {
                new() { ProviderId = providerAId, Provider = new User { Id = providerAId, Name = "Prestador A" } }
            }
        };

        var requestB = new ServiceRequest
        {
            Id = requestBId,
            ClientId = clientId,
            Client = new User { Id = clientId, Name = "Cliente" },
            Description = "Instalacao de chuveiro",
            Proposals = new List<Proposal>
            {
                new() { ProviderId = providerBId, Provider = new User { Id = providerBId, Name = "Prestador B" } }
            }
        };

        _chatRepositoryMock
            .Setup(r => r.GetConversationsByParticipantAsync(clientId, "Client"))
            .ReturnsAsync(new List<ChatMessage>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    RequestId = requestAId,
                    ProviderId = providerAId,
                    SenderId = providerAId,
                    Request = requestA,
                    Text = "Consigo atender hoje.",
                    CreatedAt = now.AddMinutes(-20),
                    ReadAt = null
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    RequestId = requestAId,
                    ProviderId = providerAId,
                    SenderId = clientId,
                    Request = requestA,
                    Text = "Perfeito, vamos fechar.",
                    CreatedAt = now.AddMinutes(-10),
                    ReadAt = now.AddMinutes(-9)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    RequestId = requestBId,
                    ProviderId = providerBId,
                    SenderId = providerBId,
                    Request = requestB,
                    Text = "Posso ir amanha cedo.",
                    CreatedAt = now.AddMinutes(-5),
                    ReadAt = null
                }
            });

        var result = await _service.GetActiveConversationsAsync(clientId, "Client");

        Assert.Equal(2, result.Count);
        Assert.Equal(requestBId, result[0].RequestId);
        Assert.Equal(providerBId, result[0].ProviderId);
        Assert.Equal("Provider", result[0].CounterpartRole);
        Assert.Equal("Prestador B", result[0].CounterpartName);
        Assert.Equal(1, result[0].UnreadMessages);

        Assert.Equal(requestAId, result[1].RequestId);
        Assert.Equal(1, result[1].UnreadMessages);
    }

    [Fact]
    public async Task GetActiveConversationsAsync_ShouldReturnEmpty_WhenRoleIsUnsupported()
    {
        var userId = Guid.NewGuid();

        var result = await _service.GetActiveConversationsAsync(userId, "Guest");

        Assert.Empty(result);
        _chatRepositoryMock.Verify(
            r => r.GetConversationsByParticipantAsync(It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never);
    }

    private static ServiceRequest CreateRequest(Guid requestId, Guid clientId, Guid providerId)
    {
        return new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Proposals = new List<Proposal>
            {
                new() { ProviderId = providerId }
            }
        };
    }
}
