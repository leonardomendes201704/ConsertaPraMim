using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminChatNotificationServiceTests
{
    private readonly Mock<IChatMessageRepository> _chatMessageRepositoryMock;
    private readonly Mock<IServiceRequestRepository> _serviceRequestRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IAdminAuditLogRepository> _auditLogRepositoryMock;
    private readonly AdminChatNotificationService _service;

    public AdminChatNotificationServiceTests()
    {
        _chatMessageRepositoryMock = new Mock<IChatMessageRepository>();
        _serviceRequestRepositoryMock = new Mock<IServiceRequestRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _notificationServiceMock = new Mock<INotificationService>();
        _auditLogRepositoryMock = new Mock<IAdminAuditLogRepository>();

        _service = new AdminChatNotificationService(
            _chatMessageRepositoryMock.Object,
            _serviceRequestRepositoryMock.Object,
            _userRepositoryMock.Object,
            _notificationServiceMock.Object,
            _auditLogRepositoryMock.Object);
    }

    [Fact(DisplayName = "Admin chat notificacao servico | Obter chats | Deve retornar conversation com masked dados")]
    public async Task GetChatsAsync_ShouldReturnConversationWithMaskedData()
    {
        var now = DateTime.UtcNow;
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        var client = new User
        {
            Id = clientId,
            Name = "Cliente",
            Email = "cliente.teste@conserta.com",
            Phone = "1199887766"
        };

        var provider = new User
        {
            Id = providerId,
            Name = "Prestador",
            Email = "prestador.teste@conserta.com",
            Phone = "1199776655"
        };

        var request = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Client = client,
            Description = "Consertar tomada",
            Status = ServiceRequestStatus.Matching
        };

        var messages = new List<ChatMessage>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RequestId = requestId,
                ProviderId = providerId,
                SenderId = providerId,
                Sender = provider,
                SenderRole = UserRole.Provider,
                Text = "Posso atender hoje.",
                Request = request,
                CreatedAt = now,
                Attachments = new List<ChatAttachment>
                {
                    new() { Id = Guid.NewGuid(), FileName = "foto.jpg", FileUrl = "/uploads/foto.jpg", MediaKind = "image", ContentType = "image/jpeg", SizeBytes = 100 }
                }
            }
        };

        _chatMessageRepositoryMock
            .Setup(r => r.GetByPeriodAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(messages);
        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { provider, client });

        var result = await _service.GetChatsAsync(new AdminChatsQueryDto(null, null, null, null, now.AddDays(-1), now.AddDays(1), 1, 10));

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal(requestId, item.RequestId);
        Assert.Equal(providerId, item.ProviderId);
        Assert.NotEqual(client.Email, item.ClientEmailMasked);
        Assert.NotEqual(provider.Phone, item.ProviderPhoneMasked);
    }

    [Fact(DisplayName = "Admin chat notificacao servico | Obter chat | Deve retornar nulo quando prestador tem no proposal for requisicao")]
    public async Task GetChatAsync_ShouldReturnNull_WhenProviderHasNoProposalForRequest()
    {
        var providerId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            Client = new User { Name = "Cliente", Email = "cliente@x.com", Phone = "11999999999" },
            Description = "Pedido sem proposta",
            Status = ServiceRequestStatus.Created,
            Proposals = new List<Proposal>
            {
                new() { ProviderId = Guid.NewGuid() }
            }
        };

        _serviceRequestRepositoryMock.Setup(r => r.GetByIdAsync(request.Id)).ReturnsAsync(request);

        var result = await _service.GetChatAsync(request.Id, providerId);

        Assert.Null(result);
    }

    [Fact(DisplayName = "Admin chat notificacao servico | Enviar notificacao | Deve enviar e audit quando payload valido")]
    public async Task SendNotificationAsync_ShouldSendAndAudit_WhenPayloadIsValid()
    {
        var actorUserId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var recipient = new User
        {
            Id = recipientId,
            Email = "Cliente.Notificado@Conserta.Com",
            IsActive = true
        };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(recipientId)).ReturnsAsync(recipient);

        var result = await _service.SendNotificationAsync(
            new AdminSendNotificationRequestDto(
                recipientId,
                "Assunto admin",
                "Mensagem importante",
                "/ServiceRequests/Details/123",
                "Suporte"),
            actorUserId,
            "admin@conserta.com");

        Assert.True(result.Success);
        _notificationServiceMock.Verify(n => n.SendNotificationAsync(
            recipientId.ToString("N"),
            "Assunto admin",
            "Mensagem importante",
            "/ServiceRequests/Details/123"), Times.Once);
        _auditLogRepositoryMock.Verify(a => a.AddAsync(It.Is<AdminAuditLog>(log =>
            log.ActorUserId == actorUserId &&
            log.Action == "ManualNotificationSent" &&
            log.TargetId == recipientId &&
            !string.IsNullOrWhiteSpace(log.Metadata) &&
            log.Metadata!.Contains("\"before\"") &&
            log.Metadata.Contains("\"after\""))), Times.Once);
    }
}
