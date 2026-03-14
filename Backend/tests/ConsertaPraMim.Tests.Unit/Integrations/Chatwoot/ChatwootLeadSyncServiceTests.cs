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

    [Fact(DisplayName = "Deve mascarar PII e segredos no ultimo erro persistido do Chatwoot")]
    public async Task DeveMascararPiiESegredosNoUltimoErroPersistidoDoChatwoot()
    {
        var kanbanService = new Mock<IAdminKanbanService>();
        var chatwootApiClient = new Mock<IChatwootApiClient>();
        var queueService = new Mock<IChatwootSyncQueueService>();
        var lead = CreateLead(18, AdminKanbanBoardTypes.Clients, phone: "(13) 99711-4422", email: "ricardo@email.com");

        kanbanService
            .Setup(service => service.GetLeadDetails(18))
            .Returns(lead);
        kanbanService
            .Setup(service => service.UpdateLeadChatwootSync(
                18,
                It.Is<AdminKanbanLeadChatwootSyncUpdateRequest>(request =>
                    request.ChatwootSyncStatus == ChatwootSyncStatuses.Failed &&
                    !string.IsNullOrWhiteSpace(request.ChatwootLastError) &&
                    !request.ChatwootLastError.Contains("ricardo@email.com", StringComparison.OrdinalIgnoreCase) &&
                    !request.ChatwootLastError.Contains("997114422", StringComparison.OrdinalIgnoreCase) &&
                    request.ChatwootLastError.Contains("r***o@email.com", StringComparison.OrdinalIgnoreCase) &&
                    request.ChatwootLastError.Contains("[redacted]", StringComparison.Ordinal))))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(18, "chatwoot_sync_falhou", It.IsAny<string>()))
            .Returns(true);

        chatwootApiClient
            .Setup(client => client.SearchContactsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ChatwootApiException("Chatwoot retornou erro HTTP 422. Resposta: email=ricardo@email.com phone=+5513997114422 token=segredo", 422));

        queueService
            .Setup(service => service.EnqueueRetry(18, ChatwootSyncOperationTypes.LeadSync, It.IsAny<string>(), false));

        var sut = CreateSut(kanbanService.Object, chatwootApiClient.Object, queueService.Object);

        var result = await sut.SyncLeadAsync(18);

        Assert.False(result.Succeeded);
        Assert.DoesNotContain("ricardo@email.com", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("997114422", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted]", result.Message, StringComparison.Ordinal);
        kanbanService.VerifyAll();
    }

    [Fact(DisplayName = "Deve enfileirar retentativa quando Chatwoot falhar por erro externo")]
    public async Task DeveEnfileirarRetentativaQuandoChatwootFalharPorErroExterno()
    {
        var kanbanService = new Mock<IAdminKanbanService>();
        var chatwootApiClient = new Mock<IChatwootApiClient>();
        var queueService = new Mock<IChatwootSyncQueueService>();
        var lead = CreateLead(19, AdminKanbanBoardTypes.Clients, phone: "(13) 99711-4422", email: "ricardo@email.com");

        kanbanService
            .Setup(service => service.GetLeadDetails(19))
            .Returns(lead);
        kanbanService
            .Setup(service => service.UpdateLeadChatwootSync(
                19,
                It.Is<AdminKanbanLeadChatwootSyncUpdateRequest>(request =>
                    request.ChatwootSyncStatus == ChatwootSyncStatuses.Failed &&
                    request.ChatwootInboxId == 1 &&
                    !string.IsNullOrWhiteSpace(request.ChatwootLastError))))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(19, "chatwoot_sync_falhou", It.IsAny<string>()))
            .Returns(true);

        chatwootApiClient
            .Setup(client => client.SearchContactsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Erro de rede"));

        queueService
            .Setup(service => service.EnqueueRetry(
                19,
                ChatwootSyncOperationTypes.LeadSync,
                It.Is<string>(message => message.Contains("rede", StringComparison.OrdinalIgnoreCase)),
                false));

        var sut = CreateSut(kanbanService.Object, chatwootApiClient.Object, queueService.Object);

        var result = await sut.SyncLeadAsync(19);

        Assert.False(result.Succeeded);
        Assert.True(result.RetrySuggested);
        Assert.True(result.QueuedForRetry);
        Assert.Contains("Retentativa automatica enfileirada", result.Message, StringComparison.Ordinal);
        queueService.VerifyAll();
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
                    request.PhoneNumber == "+5513997114422" &&
                    request.CustomAttributes.ContainsKey("cpm_lead_source") &&
                    Equals(request.CustomAttributes["cpm_lead_source"], "WhatsApp") &&
                    request.CustomAttributes.ContainsKey("cpm_lead_source_slug") &&
                    Equals(request.CustomAttributes["cpm_lead_source_slug"], "whatsapp")),
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
            .Setup(client => client.ListContactConversationsAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
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
        chatwootApiClient
            .Setup(client => client.UpdateContactAsync(
                101,
                It.Is<ChatwootUpsertContactRequest>(request =>
                    request.CustomAttributes.ContainsKey("cpm_stage_name") &&
                    Equals(request.CustomAttributes["cpm_stage_name"], "Novo lead") &&
                    request.CustomAttributes.ContainsKey("cpm_stage_slug") &&
                    Equals(request.CustomAttributes["cpm_stage_slug"], "clientes_novo_lead") &&
                    request.CustomAttributes.ContainsKey("cpm_lead_source") &&
                    Equals(request.CustomAttributes["cpm_lead_source"], "WhatsApp")),
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
            .Setup(client => client.ListContactLabelsAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.ReplaceContactLabelsAsync(
                101,
                It.Is<IReadOnlyList<string>>(labels =>
                    labels.Contains("cpm_clientes") &&
                    labels.Contains("cpm_clientes_novo_lead")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(["cpm_clientes", "cpm_clientes_novo_lead"]);
        chatwootApiClient
            .Setup(client => client.UpdateConversationCustomAttributesAsync(
                202,
                It.Is<IReadOnlyDictionary<string, object?>>(attributes =>
                    Equals(attributes["cpm_lead_id"], 21) &&
                    Equals(attributes["cpm_board_type"], AdminKanbanBoardTypes.Clients) &&
                    Equals(attributes["cpm_stage_name"], "Novo lead") &&
                    Equals(attributes["cpm_lead_source"], "WhatsApp") &&
                    Equals(attributes["cpm_lead_source_slug"], "whatsapp")),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        chatwootApiClient
            .Setup(client => client.ListConversationLabelsAsync(202, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.ReplaceConversationLabelsAsync(
                202,
                It.Is<IReadOnlyList<string>>(labels =>
                    labels.Contains("cpm_clientes") &&
                    labels.Contains("cpm_clientes_novo_lead")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "cpm_clientes", "cpm_clientes_novo_lead" });
        chatwootApiClient
            .Setup(client => client.UpdateConversationStatusAsync(202, "open", It.IsAny<CancellationToken>()))
            .ReturnsAsync("open");

        var sut = CreateSut(kanbanService.Object, chatwootApiClient.Object);

        var result = await sut.SyncLeadAsync(21);

        Assert.True(result.Succeeded);
        Assert.Equal(ChatwootSyncStatuses.Synced, result.Status);
        Assert.Equal(101, result.ContactId);
        Assert.Equal(202, result.ConversationId);
        kanbanService.VerifyAll();
        chatwootApiClient.VerifyAll();
    }

    [Fact(DisplayName = "Deve bootstrapar lead Telegram de prestador no inbox correto do Chatwoot")]
    public async Task DeveBootstraparLeadTelegramDePrestadorNoInboxCorretoDoChatwoot()
    {
        var kanbanService = new Mock<IAdminKanbanService>();
        var chatwootApiClient = new Mock<IChatwootApiClient>();
        var lead = CreateLead(
            24,
            AdminKanbanBoardTypes.Providers,
            phone: null,
            email: "prestador@email.com",
            stageName: "Novo cadastro",
            source: "Telegram");

        kanbanService
            .Setup(service => service.GetLeadDetails(24))
            .Returns(lead);
        kanbanService
            .Setup(service => service.UpdateLeadChatwootSync(
                24,
                It.Is<AdminKanbanLeadChatwootSyncUpdateRequest>(request =>
                    request.ChatwootContactId == 701 &&
                    request.ChatwootConversationId == 702 &&
                    request.ChatwootInboxId == 2 &&
                    request.ChatwootSyncStatus == ChatwootSyncStatuses.Synced &&
                    request.ClearChatwootLastError)))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(24, "chatwoot_contato_sincronizado", It.IsAny<string>()))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(24, "chatwoot_conversa_criada", It.IsAny<string>()))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(
                24,
                "chatwoot_bootstrap_via_telegram",
                It.Is<string>(message =>
                    message.Contains("Lead Telegram", StringComparison.Ordinal) &&
                    message.Contains("inbox #2", StringComparison.Ordinal))))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(24, "chatwoot_sincronizado", It.IsAny<string>()))
            .Returns(true);

        chatwootApiClient
            .Setup(client => client.SearchContactsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.CreateContactAsync(
                It.Is<ChatwootUpsertContactRequest>(request =>
                    request.InboxId == 2 &&
                    request.Email == "prestador@email.com" &&
                    request.Identifier == "email:prestador@email.com" &&
                    request.CustomAttributes.ContainsKey("cpm_board_type") &&
                    Equals(request.CustomAttributes["cpm_board_type"], AdminKanbanBoardTypes.Providers) &&
                    request.CustomAttributes.ContainsKey("cpm_stage_name") &&
                    Equals(request.CustomAttributes["cpm_stage_name"], "Novo cadastro") &&
                    request.CustomAttributes.ContainsKey("cpm_lead_source") &&
                    Equals(request.CustomAttributes["cpm_lead_source"], "Telegram") &&
                    request.CustomAttributes.ContainsKey("cpm_lead_source_slug") &&
                    Equals(request.CustomAttributes["cpm_lead_source_slug"], "telegram")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootContactSummary
            {
                Id = 701,
                Name = "Ricardo Almeida",
                Email = "prestador@email.com",
                PhoneNumber = null,
                Identifier = "email:prestador@email.com",
                ContactInboxes =
                [
                    new ChatwootContactInboxSummary
                    {
                        InboxId = 2,
                        InboxName = "CPM Prestadores",
                        SourceId = "cpm-lead-prestadores-24"
                    }
                ]
            });
        chatwootApiClient
            .Setup(client => client.ListContactConversationsAsync(701, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.CreateConversationAsync(
                It.Is<ChatwootCreateConversationRequest>(request =>
                    request.InboxId == 2 &&
                    request.ContactId == 701 &&
                    request.SourceId == "cpm-lead-prestadores-24"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootConversationSummary
            {
                Id = 702,
                InboxId = 2,
                Status = "open"
            });
        chatwootApiClient
            .Setup(client => client.CreateMessageAsync(
                702,
                It.Is<ChatwootCreateMessageRequest>(request =>
                    request.Private &&
                    request.MessageType == "outgoing" &&
                    request.Content.Contains("Funil: Funil de prestadores", StringComparison.Ordinal) &&
                    request.Content.Contains("Canal de origem: Telegram", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootMessageSummary
            {
                Id = 703,
                Private = true
            });
        chatwootApiClient
            .Setup(client => client.UpdateContactAsync(
                701,
                It.Is<ChatwootUpsertContactRequest>(request =>
                    request.CustomAttributes.ContainsKey("cpm_stage_slug") &&
                    Equals(request.CustomAttributes["cpm_stage_slug"], "prestadores_novo_cadastro") &&
                    request.CustomAttributes.ContainsKey("cpm_lead_source") &&
                    Equals(request.CustomAttributes["cpm_lead_source"], "Telegram") &&
                    request.CustomAttributes.ContainsKey("cpm_lead_source_slug") &&
                    Equals(request.CustomAttributes["cpm_lead_source_slug"], "telegram")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootContactSummary
            {
                Id = 701,
                Name = "Ricardo Almeida",
                Email = "prestador@email.com",
                PhoneNumber = null,
                Identifier = "email:prestador@email.com",
                ContactInboxes =
                [
                    new ChatwootContactInboxSummary
                    {
                        InboxId = 2,
                        InboxName = "CPM Prestadores",
                        SourceId = "cpm-lead-prestadores-24"
                    }
                ]
            });
        chatwootApiClient
            .Setup(client => client.ListContactLabelsAsync(701, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.ReplaceContactLabelsAsync(
                701,
                It.Is<IReadOnlyList<string>>(labels =>
                    labels.Contains("cpm_prestadores") &&
                    labels.Contains("cpm_prestadores_novo_cadastro")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(["cpm_prestadores", "cpm_prestadores_novo_cadastro"]);
        chatwootApiClient
            .Setup(client => client.UpdateConversationCustomAttributesAsync(
                702,
                It.Is<IReadOnlyDictionary<string, object?>>(attributes =>
                    Equals(attributes["cpm_board_type"], AdminKanbanBoardTypes.Providers) &&
                    Equals(attributes["cpm_stage_name"], "Novo cadastro") &&
                    Equals(attributes["cpm_stage_slug"], "prestadores_novo_cadastro") &&
                    Equals(attributes["cpm_lead_source"], "Telegram") &&
                    Equals(attributes["cpm_lead_source_slug"], "telegram")),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        chatwootApiClient
            .Setup(client => client.ListConversationLabelsAsync(702, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.ReplaceConversationLabelsAsync(
                702,
                It.Is<IReadOnlyList<string>>(labels =>
                    labels.Contains("cpm_prestadores") &&
                    labels.Contains("cpm_prestadores_novo_cadastro")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(["cpm_prestadores", "cpm_prestadores_novo_cadastro"]);
        chatwootApiClient
            .Setup(client => client.UpdateConversationStatusAsync(702, "open", It.IsAny<CancellationToken>()))
            .ReturnsAsync("open");

        var sut = CreateSut(kanbanService.Object, chatwootApiClient.Object);

        var result = await sut.SyncLeadAsync(24);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.InboxId);
        Assert.Equal(701, result.ContactId);
        Assert.Equal(702, result.ConversationId);
        kanbanService.VerifyAll();
        chatwootApiClient.VerifyAll();
    }

    [Fact(DisplayName = "Deve reaproveitar conversa existente do contato durante backfill sem duplicar atendimento")]
    public async Task DeveReaproveitarConversaExistenteDoContatoDuranteBackfillSemDuplicarAtendimento()
    {
        var kanbanService = new Mock<IAdminKanbanService>();
        var chatwootApiClient = new Mock<IChatwootApiClient>();
        var lead = CreateLead(27, AdminKanbanBoardTypes.Clients, phone: "(13) 99711-4422", email: "ricardo@email.com");

        kanbanService
            .Setup(service => service.GetLeadDetails(27))
            .Returns(lead);
        kanbanService
            .Setup(service => service.UpdateLeadChatwootSync(
                27,
                It.Is<AdminKanbanLeadChatwootSyncUpdateRequest>(request =>
                    request.ChatwootContactId == 101 &&
                    request.ChatwootConversationId == 808 &&
                    request.ChatwootInboxId == 1 &&
                    request.ChatwootSyncStatus == ChatwootSyncStatuses.Synced &&
                    request.ClearChatwootLastError)))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(27, "chatwoot_contato_sincronizado", It.IsAny<string>()))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(27, "chatwoot_conversa_reaproveitada", It.IsAny<string>()))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(27, "chatwoot_sincronizado", It.IsAny<string>()))
            .Returns(true);

        chatwootApiClient
            .Setup(client => client.SearchContactsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.CreateContactAsync(It.IsAny<ChatwootUpsertContactRequest>(), It.IsAny<CancellationToken>()))
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
                        SourceId = "cpm-lead-clientes-27"
                    }
                ]
            });
        chatwootApiClient
            .Setup(client => client.ListContactConversationsAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChatwootConversationSummary
                {
                    Id = 808,
                    InboxId = 1,
                    Status = "pending"
                }
            ]);
        chatwootApiClient
            .Setup(client => client.UpdateContactAsync(101, It.IsAny<ChatwootUpsertContactRequest>(), It.IsAny<CancellationToken>()))
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
                        SourceId = "cpm-lead-clientes-27"
                    }
                ]
            });
        chatwootApiClient
            .Setup(client => client.ListContactLabelsAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.ReplaceContactLabelsAsync(101, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["cpm_clientes", "cpm_clientes_novo_lead"]);
        chatwootApiClient
            .Setup(client => client.UpdateConversationCustomAttributesAsync(808, It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        chatwootApiClient
            .Setup(client => client.ListConversationLabelsAsync(808, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.ReplaceConversationLabelsAsync(808, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["cpm_clientes", "cpm_clientes_novo_lead"]);
        chatwootApiClient
            .Setup(client => client.UpdateConversationStatusAsync(808, "open", It.IsAny<CancellationToken>()))
            .ReturnsAsync("open");

        var sut = CreateSut(kanbanService.Object, chatwootApiClient.Object);

        var result = await sut.SyncLeadAsync(27);

        Assert.True(result.Succeeded);
        Assert.Equal(808, result.ConversationId);
        chatwootApiClient.Verify(client => client.CreateConversationAsync(It.IsAny<ChatwootCreateConversationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        chatwootApiClient.Verify(client => client.CreateMessageAsync(It.IsAny<long>(), It.IsAny<ChatwootCreateMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
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
                It.Is<ChatwootUpsertContactRequest>(request =>
                    request.CustomAttributes.ContainsKey("cpm_stage_name") &&
                    Equals(request.CustomAttributes["cpm_stage_name"], "Novo lead") &&
                    request.CustomAttributes.ContainsKey("cpm_stage_slug") &&
                    Equals(request.CustomAttributes["cpm_stage_slug"], "clientes_novo_lead") &&
                    request.CustomAttributes.ContainsKey("cpm_lead_source") &&
                    Equals(request.CustomAttributes["cpm_lead_source"], "WhatsApp")),
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
        chatwootApiClient
            .Setup(client => client.ListContactLabelsAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["manual_contact"]);
        chatwootApiClient
            .Setup(client => client.ReplaceContactLabelsAsync(
                101,
                It.Is<IReadOnlyList<string>>(labels =>
                    labels.Contains("manual_contact") &&
                    labels.Contains("cpm_clientes") &&
                    labels.Contains("cpm_clientes_novo_lead")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(["manual_contact", "cpm_clientes", "cpm_clientes_novo_lead"]);
        chatwootApiClient
            .Setup(client => client.UpdateConversationCustomAttributesAsync(
                202,
                It.Is<IReadOnlyDictionary<string, object?>>(attributes =>
                    Equals(attributes["cpm_lead_id"], 32) &&
                    Equals(attributes["cpm_board_type"], AdminKanbanBoardTypes.Clients) &&
                    Equals(attributes["cpm_lead_source"], "WhatsApp") &&
                    Equals(attributes["cpm_lead_source_slug"], "whatsapp")),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        chatwootApiClient
            .Setup(client => client.ListConversationLabelsAsync(202, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["manual_label"]);
        chatwootApiClient
            .Setup(client => client.ReplaceConversationLabelsAsync(
                202,
                It.Is<IReadOnlyList<string>>(labels =>
                    labels.Contains("manual_label") &&
                    labels.Contains("cpm_clientes") &&
                    labels.Contains("cpm_clientes_novo_lead")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(["manual_label", "cpm_clientes", "cpm_clientes_novo_lead"]);
        chatwootApiClient
            .Setup(client => client.UpdateConversationStatusAsync(202, "open", It.IsAny<CancellationToken>()))
            .ReturnsAsync("open");

        var sut = CreateSut(kanbanService.Object, chatwootApiClient.Object);

        var result = await sut.SyncLeadAsync(32);

        Assert.True(result.Succeeded);
        Assert.Equal(202, result.ConversationId);
        chatwootApiClient.Verify(client => client.CreateConversationAsync(It.IsAny<ChatwootCreateConversationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        chatwootApiClient.Verify(client => client.CreateMessageAsync(It.IsAny<long>(), It.IsAny<ChatwootCreateMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        kanbanService.VerifyAll();
    }

    [Fact(DisplayName = "Deve registrar bootstrap Telegram ao reaproveitar conversa existente no Chatwoot")]
    public async Task DeveRegistrarBootstrapTelegramAoReaproveitarConversaExistenteNoChatwoot()
    {
        var kanbanService = new Mock<IAdminKanbanService>();
        var chatwootApiClient = new Mock<IChatwootApiClient>();
        var lead = CreateLead(
            37,
            AdminKanbanBoardTypes.Clients,
            phone: "(13) 99711-4422",
            email: "ricardo@email.com",
            source: "Telegram");

        kanbanService
            .Setup(service => service.GetLeadDetails(37))
            .Returns(lead);
        kanbanService
            .Setup(service => service.UpdateLeadChatwootSync(
                37,
                It.Is<AdminKanbanLeadChatwootSyncUpdateRequest>(request =>
                    request.ChatwootContactId == 801 &&
                    request.ChatwootConversationId == 808 &&
                    request.ChatwootInboxId == 1 &&
                    request.ChatwootSyncStatus == ChatwootSyncStatuses.Synced &&
                    request.ClearChatwootLastError)))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(37, "chatwoot_contato_sincronizado", It.IsAny<string>()))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(37, "chatwoot_conversa_reaproveitada", It.IsAny<string>()))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(
                37,
                "chatwoot_bootstrap_via_telegram",
                It.Is<string>(message =>
                    message.Contains("reaproveitando a conversa #808", StringComparison.Ordinal) &&
                    message.Contains("inbox #1", StringComparison.Ordinal))))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(37, "chatwoot_sincronizado", It.IsAny<string>()))
            .Returns(true);

        chatwootApiClient
            .Setup(client => client.SearchContactsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.CreateContactAsync(
                It.Is<ChatwootUpsertContactRequest>(request =>
                    request.CustomAttributes.ContainsKey("cpm_lead_source") &&
                    Equals(request.CustomAttributes["cpm_lead_source"], "Telegram") &&
                    request.CustomAttributes.ContainsKey("cpm_lead_source_slug") &&
                    Equals(request.CustomAttributes["cpm_lead_source_slug"], "telegram")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootContactSummary
            {
                Id = 801,
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
                        SourceId = "cpm-lead-clientes-37"
                    }
                ]
            });
        chatwootApiClient
            .Setup(client => client.ListContactConversationsAsync(801, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChatwootConversationSummary
                {
                    Id = 808,
                    InboxId = 1,
                    Status = "pending"
                }
            ]);
        chatwootApiClient
            .Setup(client => client.UpdateContactAsync(
                801,
                It.Is<ChatwootUpsertContactRequest>(request =>
                    request.CustomAttributes.ContainsKey("cpm_lead_source") &&
                    Equals(request.CustomAttributes["cpm_lead_source"], "Telegram") &&
                    request.CustomAttributes.ContainsKey("cpm_lead_source_slug") &&
                    Equals(request.CustomAttributes["cpm_lead_source_slug"], "telegram")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootContactSummary
            {
                Id = 801,
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
                        SourceId = "cpm-lead-clientes-37"
                    }
                ]
            });
        chatwootApiClient
            .Setup(client => client.ListContactLabelsAsync(801, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.ReplaceContactLabelsAsync(801, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["cpm_clientes", "cpm_clientes_novo_lead"]);
        chatwootApiClient
            .Setup(client => client.UpdateConversationCustomAttributesAsync(
                808,
                It.Is<IReadOnlyDictionary<string, object?>>(attributes =>
                    Equals(attributes["cpm_lead_source"], "Telegram") &&
                    Equals(attributes["cpm_lead_source_slug"], "telegram")),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        chatwootApiClient
            .Setup(client => client.ListConversationLabelsAsync(808, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.ReplaceConversationLabelsAsync(808, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["cpm_clientes", "cpm_clientes_novo_lead"]);
        chatwootApiClient
            .Setup(client => client.UpdateConversationStatusAsync(808, "open", It.IsAny<CancellationToken>()))
            .ReturnsAsync("open");

        var sut = CreateSut(kanbanService.Object, chatwootApiClient.Object);

        var result = await sut.SyncLeadAsync(37);

        Assert.True(result.Succeeded);
        Assert.Equal(808, result.ConversationId);
        chatwootApiClient.Verify(client => client.CreateConversationAsync(It.IsAny<ChatwootCreateConversationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        chatwootApiClient.Verify(client => client.CreateMessageAsync(It.IsAny<long>(), It.IsAny<ChatwootCreateMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        kanbanService.VerifyAll();
        chatwootApiClient.VerifyAll();
    }

    [Fact(DisplayName = "Deve sincronizar etapa do lead com status, labels e atributos da conversa")]
    public async Task DeveSincronizarEtapaDoLeadComStatusLabelsEAtributosDaConversa()
    {
        var kanbanService = new Mock<IAdminKanbanService>();
        var chatwootApiClient = new Mock<IChatwootApiClient>();
        var lead = CreateLead(
            45,
            AdminKanbanBoardTypes.Clients,
            phone: "(13) 99711-4422",
            email: "ricardo@email.com",
            stageName: "Tentativa de contato",
            chatwoot: new AdminKanbanLeadChatwootSyncRecord
            {
                ContactId = 101,
                ConversationId = 909,
                InboxId = 1,
                SyncStatus = ChatwootSyncStatuses.Synced
            });

        kanbanService
            .Setup(service => service.GetLeadDetails(45))
            .Returns(lead);
        kanbanService
            .Setup(service => service.UpdateLeadChatwootSync(
                45,
                It.Is<AdminKanbanLeadChatwootSyncUpdateRequest>(request =>
                    request.ChatwootContactId == 101 &&
                    request.ChatwootConversationId == 909 &&
                    request.ChatwootInboxId == 1 &&
                    request.ChatwootSyncStatus == ChatwootSyncStatuses.Synced &&
                    request.ClearChatwootLastError)))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(
                45,
                "chatwoot_etapa_sincronizada",
                It.Is<string>(message => message.Contains("Tentativa de contato", StringComparison.Ordinal))))
            .Returns(true);

        chatwootApiClient
            .Setup(client => client.UpdateContactAsync(
                101,
                It.Is<ChatwootUpsertContactRequest>(request =>
                    request.CustomAttributes.ContainsKey("cpm_stage_name") &&
                    Equals(request.CustomAttributes["cpm_stage_name"], "Tentativa de contato") &&
                    request.CustomAttributes.ContainsKey("cpm_stage_slug") &&
                    Equals(request.CustomAttributes["cpm_stage_slug"], "clientes_tentativa_de_contato") &&
                    request.CustomAttributes.ContainsKey("cpm_lead_source") &&
                    Equals(request.CustomAttributes["cpm_lead_source"], "WhatsApp")),
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
                        SourceId = "cpm-lead-clientes-45"
                    }
                ]
            });
        chatwootApiClient
            .Setup(client => client.ListContactLabelsAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["vip"]);
        chatwootApiClient
            .Setup(client => client.ReplaceContactLabelsAsync(
                101,
                It.Is<IReadOnlyList<string>>(labels =>
                    labels.Contains("vip") &&
                    labels.Contains("cpm_clientes") &&
                    labels.Contains("cpm_clientes_tentativa_de_contato") &&
                    !labels.Contains("cpm_clientes_novo_lead")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(["vip", "cpm_clientes", "cpm_clientes_tentativa_de_contato"]);
        chatwootApiClient
            .Setup(client => client.UpdateConversationCustomAttributesAsync(
                909,
                It.Is<IReadOnlyDictionary<string, object?>>(attributes =>
                    Equals(attributes["cpm_stage_name"], "Tentativa de contato") &&
                    Equals(attributes["cpm_stage_slug"], "clientes_tentativa_de_contato") &&
                    Equals(attributes["cpm_lead_source"], "WhatsApp") &&
                    Equals(attributes["cpm_lead_source_slug"], "whatsapp")),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        chatwootApiClient
            .Setup(client => client.ListConversationLabelsAsync(909, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["manual", "cpm_clientes_novo_lead"]);
        chatwootApiClient
            .Setup(client => client.ReplaceConversationLabelsAsync(
                909,
                It.Is<IReadOnlyList<string>>(labels =>
                    labels.Contains("manual") &&
                    labels.Contains("cpm_clientes") &&
                    labels.Contains("cpm_clientes_tentativa_de_contato") &&
                    !labels.Contains("cpm_clientes_novo_lead")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(["manual", "cpm_clientes", "cpm_clientes_tentativa_de_contato"]);
        chatwootApiClient
            .Setup(client => client.UpdateConversationStatusAsync(909, "pending", It.IsAny<CancellationToken>()))
            .ReturnsAsync("pending");
        chatwootApiClient
            .Setup(client => client.CreateMessageAsync(
                909,
                It.Is<ChatwootCreateMessageRequest>(request =>
                    request.Private &&
                    request.MessageType == "outgoing" &&
                    request.Content.Contains("Atualizacao de etapa", StringComparison.Ordinal) &&
                    request.Content.Contains("Tentativa de contato", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootMessageSummary
            {
                Id = 111,
                Private = true
            });

        var sut = CreateSut(kanbanService.Object, chatwootApiClient.Object);

        var result = await sut.SyncLeadStageAsync(45);

        Assert.True(result.Succeeded);
        Assert.Equal(ChatwootSyncStatuses.Synced, result.Status);
        kanbanService.VerifyAll();
        chatwootApiClient.VerifyAll();
    }

    [Fact(DisplayName = "Deve normalizar canal do lead para atributos visiveis no Chatwoot")]
    public async Task DeveNormalizarCanalDoLeadParaAtributosVisiveisNoChatwoot()
    {
        var kanbanService = new Mock<IAdminKanbanService>();
        var chatwootApiClient = new Mock<IChatwootApiClient>();
        var lead = CreateLead(
            58,
            AdminKanbanBoardTypes.Clients,
            phone: "(13) 99711-4422",
            email: "ricardo@email.com",
            source: "Instagram Ads");

        kanbanService
            .Setup(service => service.GetLeadDetails(58))
            .Returns(lead);
        kanbanService
            .Setup(service => service.UpdateLeadChatwootSync(
                58,
                It.Is<AdminKanbanLeadChatwootSyncUpdateRequest>(request =>
                    request.ChatwootContactId == 501 &&
                    request.ChatwootConversationId == 502 &&
                    request.ChatwootInboxId == 1 &&
                    request.ChatwootSyncStatus == ChatwootSyncStatuses.Synced &&
                    request.ClearChatwootLastError)))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(58, "chatwoot_contato_sincronizado", It.IsAny<string>()))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(58, "chatwoot_conversa_criada", It.IsAny<string>()))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(58, "chatwoot_sincronizado", It.IsAny<string>()))
            .Returns(true);

        chatwootApiClient
            .Setup(client => client.SearchContactsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.CreateContactAsync(
                It.Is<ChatwootUpsertContactRequest>(request =>
                    request.CustomAttributes.ContainsKey("cpm_lead_source") &&
                    Equals(request.CustomAttributes["cpm_lead_source"], "Instagram") &&
                    request.CustomAttributes.ContainsKey("cpm_lead_source_slug") &&
                    Equals(request.CustomAttributes["cpm_lead_source_slug"], "instagram") &&
                    request.AdditionalAttributes.ContainsKey("source") &&
                    Equals(request.AdditionalAttributes["source"], "Instagram Ads") &&
                    request.AdditionalAttributes.ContainsKey("source_display") &&
                    Equals(request.AdditionalAttributes["source_display"], "Instagram")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootContactSummary
            {
                Id = 501,
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
                        SourceId = "cpm-lead-clientes-58"
                    }
                ]
            });
        chatwootApiClient
            .Setup(client => client.ListContactConversationsAsync(501, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.CreateConversationAsync(It.IsAny<ChatwootCreateConversationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootConversationSummary
            {
                Id = 502,
                InboxId = 1,
                Status = "open"
            });
        chatwootApiClient
            .Setup(client => client.CreateMessageAsync(
                502,
                It.Is<ChatwootCreateMessageRequest>(request =>
                    request.Content.Contains("Canal de origem: Instagram", StringComparison.Ordinal) &&
                    request.Content.Contains("Fonte original informada: Instagram Ads", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootMessageSummary
            {
                Id = 503,
                Private = true
            });
        chatwootApiClient
            .Setup(client => client.UpdateContactAsync(
                501,
                It.Is<ChatwootUpsertContactRequest>(request =>
                    request.CustomAttributes.ContainsKey("cpm_lead_source") &&
                    Equals(request.CustomAttributes["cpm_lead_source"], "Instagram") &&
                    request.CustomAttributes.ContainsKey("cpm_lead_source_slug") &&
                    Equals(request.CustomAttributes["cpm_lead_source_slug"], "instagram")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootContactSummary
            {
                Id = 501,
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
                        SourceId = "cpm-lead-clientes-58"
                    }
                ]
            });
        chatwootApiClient
            .Setup(client => client.ListContactLabelsAsync(501, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.ReplaceContactLabelsAsync(501, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["cpm_clientes", "cpm_clientes_novo_lead"]);
        chatwootApiClient
            .Setup(client => client.UpdateConversationCustomAttributesAsync(
                502,
                It.Is<IReadOnlyDictionary<string, object?>>(attributes =>
                    Equals(attributes["cpm_lead_source"], "Instagram") &&
                    Equals(attributes["cpm_lead_source_slug"], "instagram")),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        chatwootApiClient
            .Setup(client => client.ListConversationLabelsAsync(502, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        chatwootApiClient
            .Setup(client => client.ReplaceConversationLabelsAsync(502, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["cpm_clientes", "cpm_clientes_novo_lead"]);
        chatwootApiClient
            .Setup(client => client.UpdateConversationStatusAsync(502, "open", It.IsAny<CancellationToken>()))
            .ReturnsAsync("open");

        var sut = CreateSut(kanbanService.Object, chatwootApiClient.Object);

        var result = await sut.SyncLeadAsync(58);

        Assert.True(result.Succeeded);
        kanbanService.VerifyAll();
        chatwootApiClient.VerifyAll();
    }

    private static ChatwootLeadSyncService CreateSut(
        IAdminKanbanService kanbanService,
        IChatwootApiClient chatwootApiClient,
        IChatwootSyncQueueService? chatwootSyncQueueService = null,
        bool enabled = true)
    {
        var options = Options.Create(new ChatwootOptions
        {
            Enabled = enabled,
            BaseUrl = "https://chatwoot.exemplo.com",
            ApiAccessToken = "token",
            AccountId = 1,
            ClientsInboxId = 1,
            ProvidersInboxId = 2,
            WebhookSecret = "secret",
            RetryWorkerEnabled = true,
            RetryWorkerIntervalSeconds = 30,
            RetryWorkerBatchSize = 20,
            SyncQueueMaxAttempts = 10,
            WebhookPayloadRetentionDays = 14,
            WebhookPayloadCleanupIntervalMinutes = 360
        });

        return new ChatwootLeadSyncService(
            kanbanService,
            chatwootApiClient,
            chatwootSyncQueueService ?? Mock.Of<IChatwootSyncQueueService>(),
            options,
            NullLogger<ChatwootLeadSyncService>.Instance);
    }

    private static AdminKanbanLeadDetailsRecord CreateLead(
        int leadId,
        string boardType,
        string? phone,
        string? email,
        string stageName = "Novo lead",
        string source = "WhatsApp",
        AdminKanbanLeadChatwootSyncRecord? chatwoot = null) =>
        new()
        {
            Id = leadId,
            StageId = 1,
            StageName = stageName,
            BoardType = boardType,
            Name = "Ricardo Almeida",
            Phone = phone ?? string.Empty,
            Email = email ?? string.Empty,
            ServiceCategory = "Eletricista",
            PostalCode = "11701-200",
            City = "Praia Grande",
            Source = source,
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
