using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Chatwoot;

public sealed class ChatwootLeadSyncServiceTests
{
    [Fact(DisplayName = "Deve falhar ao sincronizar lead sem telefone e sem e-mail")]
    public async Task DeveFalharAoSincronizarLeadSemTelefoneESemEmail()
    {
        var kanbanService = new Mock<IAdminKanbanService>();
        var chatwootApiClient = new Mock<IChatwootApiClient>(MockBehavior.Strict);

        kanbanService
            .Setup(service => service.GetLeadDetails(10))
            .Returns(CreateLead(10, AdminKanbanBoardTypes.Clients, phone: null, email: null));
        kanbanService
            .Setup(service => service.UpdateLeadChatwootSync(
                10,
                It.Is<AdminKanbanLeadChatwootSyncUpdateRequest>(request =>
                    request.ChatwootSyncStatus == ChatwootSyncStatuses.Failed &&
                    request.ChatwootInboxId == 1 &&
                    !string.IsNullOrWhiteSpace(request.ChatwootLastError))))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(
                10,
                "chatwoot_sync_falhou",
                It.Is<string>(message => message.Contains("telefone", StringComparison.OrdinalIgnoreCase))))
            .Returns(true);

        var sut = CreateSut(kanbanService.Object, chatwootApiClient.Object);

        var result = await sut.SyncLeadAsync(10);

        Assert.False(result.Succeeded);
        Assert.Equal(ChatwootSyncStatuses.Failed, result.Status);
        kanbanService.VerifyAll();
        chatwootApiClient.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "Deve criar contato e conversa no Chatwoot para lead novo")]
    public async Task DeveCriarContatoEConversaNoChatwootParaLeadNovo()
    {
        var kanbanService = new Mock<IAdminKanbanService>();
        var chatwootApiClient = new Mock<IChatwootApiClient>();
        var lead = CreateLead(21, AdminKanbanBoardTypes.Clients, phone: "(13) 99711-4422", email: "ricardo@email.com");

        kanbanService
            .Setup(service => service.GetLeadDetails(21))
            .Returns(lead);
        kanbanService
            .Setup(service => service.UpdateLeadChatwootSync(
                21,
                It.Is<AdminKanbanLeadChatwootSyncUpdateRequest>(request =>
                    request.ChatwootContactId == 101 &&
                    request.ChatwootConversationId == 202 &&
                    request.ChatwootInboxId == 1 &&
                    request.ChatwootSyncStatus == ChatwootSyncStatuses.Synced &&
                    request.ClearChatwootLastError)))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(21, "chatwoot_contato_sincronizado", It.IsAny<string>()))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(21, "chatwoot_conversa_criada", It.IsAny<string>()))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(21, "chatwoot_sincronizado", It.IsAny<string>()))
            .Returns(true);

        chatwootApiClient
            .Setup(client => client.SearchContactsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.CreateContactAsync(
                It.Is<ChatwootUpsertContactRequest>(request =>
                    request.InboxId == 1 &&
                    request.Identifier.StartsWith("phone:+5513997114422", StringComparison.Ordinal) &&
                    request.PhoneNumber == "+5513997114422"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootContactSummary
            {
                Id = 101,
                Name = "Ricardo Almeida",
                Email = "ricardo@email.com",
                PhoneNumber = "+5513997114422",
                Identifier = "phone:+5513997114422",
                ContactInboxes =
                [
                    new ChatwootContactInboxSummary
                    {
                        InboxId = 1,
                        InboxName = "CPM Clientes",
                        SourceId = "cpm-lead-clientes-21"
                    }
                ]
            });
        chatwootApiClient
            .Setup(client => client.CreateConversationAsync(
                It.Is<ChatwootCreateConversationRequest>(request =>
                    request.InboxId == 1 &&
                    request.ContactId == 101 &&
                    request.SourceId == "cpm-lead-clientes-21"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootConversationSummary
            {
                Id = 202,
                InboxId = 1,
                Status = "open"
            });
        chatwootApiClient
            .Setup(client => client.CreateMessageAsync(
                202,
                It.Is<ChatwootCreateMessageRequest>(request =>
                    request.Private &&
                    request.MessageType == "outgoing" &&
                    request.Content.Contains("Ricardo Almeida", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootMessageSummary
            {
                Id = 303,
                Private = true
            });

        var sut = CreateSut(kanbanService.Object, chatwootApiClient.Object);

        var result = await sut.SyncLeadAsync(21);

        Assert.True(result.Succeeded);
        Assert.Equal(ChatwootSyncStatuses.Synced, result.Status);
        Assert.Equal(101, result.ContactId);
        Assert.Equal(202, result.ConversationId);
        kanbanService.VerifyAll();
        chatwootApiClient.VerifyAll();
    }

    [Fact(DisplayName = "Deve reaproveitar conversa existente ao reprocessar lead sincronizado parcialmente")]
    public async Task DeveReaproveitarConversaExistenteAoReprocessarLeadSincronizadoParcialmente()
    {
        var kanbanService = new Mock<IAdminKanbanService>();
        var chatwootApiClient = new Mock<IChatwootApiClient>();
        var lead = CreateLead(
            32,
            AdminKanbanBoardTypes.Clients,
            phone: "(13) 99711-4422",
            email: "ricardo@email.com",
            chatwoot: new AdminKanbanLeadChatwootSyncRecord
            {
                ContactId = 101,
                ConversationId = 202,
                InboxId = 1,
                SyncStatus = ChatwootSyncStatuses.Failed,
                LastError = "Falha anterior"
            });

        kanbanService
            .Setup(service => service.GetLeadDetails(32))
            .Returns(lead);
        kanbanService
            .Setup(service => service.UpdateLeadChatwootSync(
                32,
                It.Is<AdminKanbanLeadChatwootSyncUpdateRequest>(request =>
                    request.ChatwootContactId == 101 &&
                    request.ChatwootConversationId == 202 &&
                    request.ChatwootInboxId == 1 &&
                    request.ChatwootSyncStatus == ChatwootSyncStatuses.Synced &&
                    request.ClearChatwootLastError)))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(32, "chatwoot_sincronizado", It.IsAny<string>()))
            .Returns(true);

        chatwootApiClient
            .Setup(client => client.GetContactAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootContactSummary
            {
                Id = 101,
                Name = "Ricardo Almeida",
                Email = "ricardo@email.com",
                PhoneNumber = "+5513997114422",
                Identifier = "phone:+5513997114422",
                ContactInboxes =
                [
                    new ChatwootContactInboxSummary
                    {
                        InboxId = 1,
                        InboxName = "CPM Clientes",
                        SourceId = "cpm-lead-clientes-32"
                    }
                ]
            });
        chatwootApiClient
            .Setup(client => client.UpdateContactAsync(
                101,
                It.IsAny<ChatwootUpsertContactRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootContactSummary
            {
                Id = 101,
                Name = "Ricardo Almeida",
                Email = "ricardo@email.com",
                PhoneNumber = "+5513997114422",
                Identifier = "phone:+5513997114422",
                ContactInboxes =
                [
                    new ChatwootContactInboxSummary
                    {
                        InboxId = 1,
                        InboxName = "CPM Clientes",
                        SourceId = "cpm-lead-clientes-32"
                    }
                ]
            });

        var sut = CreateSut(kanbanService.Object, chatwootApiClient.Object);

        var result = await sut.SyncLeadAsync(32);

        Assert.True(result.Succeeded);
        Assert.Equal(202, result.ConversationId);
        chatwootApiClient.Verify(client => client.CreateConversationAsync(It.IsAny<ChatwootCreateConversationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        chatwootApiClient.Verify(client => client.CreateMessageAsync(It.IsAny<long>(), It.IsAny<ChatwootCreateMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        kanbanService.VerifyAll();
    }

    private static ChatwootLeadSyncService CreateSut(IAdminKanbanService kanbanService, IChatwootApiClient chatwootApiClient, bool enabled = true)
    {
        var options = Options.Create(new ChatwootOptions
        {
            Enabled = enabled,
            BaseUrl = "https://chatwoot.exemplo.com",
            ApiAccessToken = "token",
            AccountId = 1,
            ClientsInboxId = 1,
            ProvidersInboxId = 2,
            WebhookSecret = "secret"
        });

        return new ChatwootLeadSyncService(
            kanbanService,
            chatwootApiClient,
            options,
            NullLogger<ChatwootLeadSyncService>.Instance);
    }

    private static AdminKanbanLeadDetailsRecord CreateLead(
        int leadId,
        string boardType,
        string? phone,
        string? email,
        AdminKanbanLeadChatwootSyncRecord? chatwoot = null) =>
        new()
        {
            Id = leadId,
            StageId = 1,
            StageName = "Novo lead",
            BoardType = boardType,
            Name = "Ricardo Almeida",
            Phone = phone ?? string.Empty,
            Email = email ?? string.Empty,
            ServiceCategory = "Eletricista",
            PostalCode = "11701-200",
            City = "Praia Grande",
            Source = "WhatsApp",
            Priority = "normal",
            StatusNote = "Lead vindo do canal de atendimento.",
            InternalNotes = string.Empty,
            CreatedAt = new DateTime(2026, 3, 13, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 3, 13, 12, 5, 0, DateTimeKind.Utc),
            LastContactAt = null,
            Chatwoot = chatwoot ?? new AdminKanbanLeadChatwootSyncRecord(),
            History = []
        };
}
