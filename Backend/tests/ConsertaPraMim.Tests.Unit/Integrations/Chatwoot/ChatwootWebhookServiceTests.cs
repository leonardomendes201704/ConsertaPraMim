using System.Security.Cryptography;
using System.Text;
using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Chatwoot;

public sealed class ChatwootWebhookServiceTests
{
    [Fact(DisplayName = "Deve processar mensagem recebida do webhook e atualizar lead")]
    public async Task DeveProcessarMensagemRecebidaDoWebhookEAtualizarLead()
    {
        var kanbanService = new Mock<IAdminKanbanService>();
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var rawPayload = Encoding.UTF8.GetBytes($$"""
{
  "event": "message_created",
  "content": "Preciso de retorno sobre o atendimento.",
  "message_type": "incoming",
  "private": false,
  "created_at": {{occurredAt}},
  "conversation": { "id": 202 },
  "sender": { "type": "contact", "name": "Ricardo Almeida" }
}
""");

        kanbanService
            .Setup(service => service.CreateOrGetChatwootWebhookEvent(
                It.Is<AdminKanbanChatwootWebhookEventUpsertRequest>(request =>
                    request.ProviderEventId == "delivery-1" &&
                    request.EventType == "message_created" &&
                    request.ConversationId == 202)))
            .Returns(new AdminKanbanChatwootWebhookEventRecord
            {
                Id = 900,
                ProviderEventId = "delivery-1",
                EventType = "message_created",
                ConversationId = 202,
                ProcessStatus = "received",
                ReceivedAt = DateTime.UtcNow
            });
        kanbanService
            .Setup(service => service.FindLeadIdByChatwootConversationId(202))
            .Returns(45);
        kanbanService
            .Setup(service => service.ApplyChatwootWebhookLeadUpdate(
                45,
                It.Is<AdminKanbanLeadWebhookUpdateRequest>(request =>
                    request.HistoryEventType == "chatwoot_mensagem_recebida" &&
                    request.LastContactAt.HasValue &&
                    request.DescriptionContains("Resumo: Preciso de retorno sobre o atendimento."))))
            .Returns(true);
        kanbanService
            .Setup(service => service.CompleteChatwootWebhookEvent(900, "processed", null))
            .Returns(true);

        var sut = CreateSut(kanbanService.Object);

        var result = await sut.HandleAsync(new ChatwootWebhookRequest
        {
            RawBody = rawPayload,
            Timestamp = timestamp,
            Signature = ComputeSignature("secret-webhook", timestamp, rawPayload),
            DeliveryId = "delivery-1"
        });

        Assert.True(result.Accepted);
        Assert.Equal("processed", result.ProcessStatus);
        Assert.Equal(45, result.LeadId);
        kanbanService.VerifyAll();
    }

    [Fact(DisplayName = "Deve rejeitar webhook com assinatura invalida")]
    public async Task DeveRejeitarWebhookComAssinaturaInvalida()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var rawPayload = Encoding.UTF8.GetBytes("""{"event":"message_created","conversation":{"id":202}}""");

        var sut = CreateSut(kanbanService.Object);

        var result = await sut.HandleAsync(new ChatwootWebhookRequest
        {
            RawBody = rawPayload,
            Timestamp = timestamp,
            Signature = "assinatura-invalida",
            DeliveryId = "delivery-invalid"
        });

        Assert.False(result.Accepted);
        Assert.Equal(401, result.HttpStatusCode);
        kanbanService.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "Deve ignorar evento duplicado por delivery id")]
    public async Task DeveIgnorarEventoDuplicadoPorDeliveryId()
    {
        var kanbanService = new Mock<IAdminKanbanService>();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var rawPayload = Encoding.UTF8.GetBytes("""{"event":"conversation_updated","id":202}""");

        kanbanService
            .Setup(service => service.CreateOrGetChatwootWebhookEvent(It.IsAny<AdminKanbanChatwootWebhookEventUpsertRequest>()))
            .Returns(new AdminKanbanChatwootWebhookEventRecord
            {
                Id = 901,
                ProviderEventId = "delivery-duplicate",
                EventType = "conversation_updated",
                ConversationId = 202,
                ProcessStatus = "processed",
                ReceivedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow,
                IsDuplicate = true
            });

        var sut = CreateSut(kanbanService.Object);

        var result = await sut.HandleAsync(new ChatwootWebhookRequest
        {
            RawBody = rawPayload,
            Timestamp = timestamp,
            Signature = ComputeSignature("secret-webhook", timestamp, rawPayload),
            DeliveryId = "delivery-duplicate"
        });

        Assert.True(result.Accepted);
        Assert.Equal("duplicate", result.ProcessStatus);
        Assert.True(result.IsDuplicate);
        kanbanService.Verify(service => service.CreateOrGetChatwootWebhookEvent(It.IsAny<AdminKanbanChatwootWebhookEventUpsertRequest>()), Times.Once);
        kanbanService.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "Deve aplicar idempotencia por assinatura quando delivery id nao vier no webhook")]
    public async Task DeveAplicarIdempotenciaPorAssinaturaQuandoDeliveryIdNaoVierNoWebhook()
    {
        var kanbanService = new Mock<IAdminKanbanService>();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var rawPayload = Encoding.UTF8.GetBytes("""{"event":"conversation_updated","id":203,"changed_attributes":["status"]}""");
        var signature = ComputeSignature("secret-webhook", timestamp, rawPayload);

        kanbanService
            .Setup(service => service.CreateOrGetChatwootWebhookEvent(
                It.Is<AdminKanbanChatwootWebhookEventUpsertRequest>(request =>
                    request.ProviderEventId == $"sig:{timestamp}:{signature.Replace("sha256=", string.Empty, StringComparison.Ordinal)}" &&
                    request.Signature == signature)))
            .Returns(new AdminKanbanChatwootWebhookEventRecord
            {
                Id = 903,
                ProviderEventId = $"sig:{timestamp}:{signature.Replace("sha256=", string.Empty, StringComparison.Ordinal)}",
                EventType = "conversation_updated",
                ConversationId = 203,
                ProcessStatus = "processed",
                ReceivedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow,
                IsDuplicate = true
            });

        var sut = CreateSut(kanbanService.Object);

        var result = await sut.HandleAsync(new ChatwootWebhookRequest
        {
            RawBody = rawPayload,
            Timestamp = timestamp,
            Signature = signature
        });

        Assert.True(result.Accepted);
        Assert.Equal("duplicate", result.ProcessStatus);
        Assert.True(result.IsDuplicate);
        kanbanService.VerifyAll();
        kanbanService.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "Deve rejeitar webhook quando origem nao estiver na allowlist")]
    public async Task DeveRejeitarWebhookQuandoOrigemNaoEstiverNaAllowlist()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var rawPayload = Encoding.UTF8.GetBytes("""{"event":"message_created","conversation":{"id":202}}""");
        var sut = CreateSut(kanbanService.Object, allowedWebhookIps: "10.0.0.0/24");

        var result = await sut.HandleAsync(new ChatwootWebhookRequest
        {
            RawBody = rawPayload,
            Timestamp = timestamp,
            Signature = ComputeSignature("secret-webhook", timestamp, rawPayload),
            DeliveryId = "delivery-allowlist",
            RemoteIp = "187.77.48.150"
        });

        Assert.False(result.Accepted);
        Assert.Equal(403, result.HttpStatusCode);
        Assert.Equal("rejected", result.ProcessStatus);
        kanbanService.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "Deve registrar historico quando status da conversa muda no Chatwoot")]
    public async Task DeveRegistrarHistoricoQuandoStatusDaConversaMudaNoChatwoot()
    {
        var kanbanService = new Mock<IAdminKanbanService>();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var rawPayload = Encoding.UTF8.GetBytes("""
{
  "event": "conversation_status_changed",
  "id": 777,
  "status": "pending"
}
""");

        kanbanService
            .Setup(service => service.CreateOrGetChatwootWebhookEvent(It.IsAny<AdminKanbanChatwootWebhookEventUpsertRequest>()))
            .Returns(new AdminKanbanChatwootWebhookEventRecord
            {
                Id = 902,
                ProviderEventId = "delivery-status",
                EventType = "conversation_status_changed",
                ConversationId = 777,
                ProcessStatus = "received",
                ReceivedAt = DateTime.UtcNow
            });
        kanbanService
            .Setup(service => service.FindLeadIdByChatwootConversationId(777))
            .Returns(51);
        kanbanService
            .Setup(service => service.ApplyChatwootWebhookLeadUpdate(
                51,
                It.Is<AdminKanbanLeadWebhookUpdateRequest>(request =>
                    !request.LastContactAt.HasValue &&
                    request.HistoryEventType == "chatwoot_status_alterado" &&
                    request.HistoryDescription.Contains("pendente", StringComparison.OrdinalIgnoreCase))))
            .Returns(true);
        kanbanService
            .Setup(service => service.CompleteChatwootWebhookEvent(902, "processed", null))
            .Returns(true);

        var sut = CreateSut(kanbanService.Object);

        var result = await sut.HandleAsync(new ChatwootWebhookRequest
        {
            RawBody = rawPayload,
            Timestamp = timestamp,
            Signature = ComputeSignature("secret-webhook", timestamp, rawPayload),
            DeliveryId = "delivery-status"
        });

        Assert.True(result.Accepted);
        Assert.Equal("processed", result.ProcessStatus);
        Assert.Equal(51, result.LeadId);
        kanbanService.VerifyAll();
    }

    private static ChatwootWebhookService CreateSut(IAdminKanbanService kanbanService, string? allowedWebhookIps = null)
    {
        var options = Options.Create(new ChatwootOptions
        {
            Enabled = true,
            BaseUrl = "https://chatwoot.exemplo.com",
            ApiAccessToken = "token",
            AccountId = 1,
            ClientsInboxId = 1,
            ProvidersInboxId = 2,
            WebhookSecret = "secret-webhook",
            AllowedWebhookIps = allowedWebhookIps ?? string.Empty,
            WebhookPayloadRetentionDays = 14,
            WebhookPayloadCleanupIntervalMinutes = 360
        });

        return new ChatwootWebhookService(
            kanbanService,
            options,
            NullLogger<ChatwootWebhookService>.Instance);
    }

    private static string ComputeSignature(string secret, string timestamp, byte[] rawPayload)
    {
        var signedPayload = $"{timestamp}.{Encoding.UTF8.GetString(rawPayload)}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return $"sha256={Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload))).ToLowerInvariant()}";
    }
}

internal static class AdminKanbanLeadWebhookUpdateRequestAssertions
{
    public static bool DescriptionContains(this AdminKanbanLeadWebhookUpdateRequest request, string expectedFragment) =>
        request.HistoryDescription.Contains(expectedFragment, StringComparison.Ordinal);
}
