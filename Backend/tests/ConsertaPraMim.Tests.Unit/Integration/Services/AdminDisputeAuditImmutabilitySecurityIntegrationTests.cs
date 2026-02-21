using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integration.Services;

public class AdminDisputeAuditImmutabilitySecurityIntegrationTests
{
    /// <summary>
    /// Cenario: admin evolui workflow de uma disputa ja auditada e a trilha precisa ser imutavel.
    /// Passos: cria disputa aberta com auditorias iniciais, executa UpdateWorkflowAsync e recarrega os registros originais.
    /// Resultado esperado: entradas anteriores permanecem intactas e novas entradas sao anexadas, sem sobrescrever historico.
    /// </summary>
    [Fact(DisplayName = "Admin dispute audit immutability security integracao | Atualizar workflow | Deve append audit trail sem mutating previous entries")]
    public async Task UpdateWorkflowAsync_ShouldAppendAuditTrail_WithoutMutatingPreviousEntries()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var seeded = await SeedOpenDisputeAsync(context);
            var service = BuildService(context);

            var initialCaseAudit = await context.ServiceDisputeCaseAuditEntries.FindAsync(seeded.InitialCaseAuditEntryId);
            var initialAdminAudit = await context.AdminAuditLogs.FindAsync(seeded.InitialAdminAuditLogId);
            Assert.NotNull(initialCaseAudit);
            Assert.NotNull(initialAdminAudit);

            var initialCaseMessage = initialCaseAudit!.Message;
            var initialCaseMetadata = initialCaseAudit.MetadataJson;
            var initialAdminMetadata = initialAdminAudit!.Metadata;
            var initialCaseCreatedAt = initialCaseAudit.CreatedAt;
            var initialAdminCreatedAt = initialAdminAudit.CreatedAt;

            var result = await service.UpdateWorkflowAsync(
                seeded.DisputeCaseId,
                seeded.AdminUserId,
                "admin.security@teste.com",
                new AdminUpdateDisputeWorkflowRequestDto(
                    Status: "UnderReview",
                    WaitingForRole: null,
                    Note: "Triagem de seguranca",
                    ClaimOwnership: true));

            Assert.True(result.Success, result.ErrorMessage);

            var reloadedCaseAudit = await context.ServiceDisputeCaseAuditEntries.FindAsync(seeded.InitialCaseAuditEntryId);
            var reloadedAdminAudit = await context.AdminAuditLogs.FindAsync(seeded.InitialAdminAuditLogId);
            Assert.NotNull(reloadedCaseAudit);
            Assert.NotNull(reloadedAdminAudit);

            Assert.Equal(initialCaseMessage, reloadedCaseAudit!.Message);
            Assert.Equal(initialCaseMetadata, reloadedCaseAudit.MetadataJson);
            Assert.Equal(initialCaseCreatedAt, reloadedCaseAudit.CreatedAt);

            Assert.Equal(initialAdminMetadata, reloadedAdminAudit!.Metadata);
            Assert.Equal(initialAdminCreatedAt, reloadedAdminAudit.CreatedAt);

            var caseAuditCount = await context.ServiceDisputeCaseAuditEntries
                .CountAsync(x => x.ServiceDisputeCaseId == seeded.DisputeCaseId);
            var adminAuditCount = await context.AdminAuditLogs
                .CountAsync(x => x.TargetType == "ServiceDisputeCase" && x.TargetId == seeded.DisputeCaseId);

            Assert.Equal(2, caseAuditCount);
            Assert.Equal(2, adminAuditCount);
        }
    }

    /// <summary>
    /// Cenario: rotina de retencao LGPD processa disputa antiga ja encerrada com trilhas preexistentes.
    /// Passos: semeia caso resolvido e antigo, executa RunRetentionAsync fora de dry-run e reconsulta auditorias originais.
    /// Resultado esperado: retencao gera eventos adicionais de anonimização e mantem imutaveis os registros historicos anteriores.
    /// </summary>
    [Fact(DisplayName = "Admin dispute audit immutability security integracao | Run retention | Deve append retention events sem mutating previous audit entries")]
    public async Task RunRetentionAsync_ShouldAppendRetentionEvents_WithoutMutatingPreviousAuditEntries()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var seeded = await SeedClosedDisputeAsync(context);
            var service = BuildService(context);

            var initialCaseAudit = await context.ServiceDisputeCaseAuditEntries.FindAsync(seeded.InitialCaseAuditEntryId);
            var initialAdminAudit = await context.AdminAuditLogs.FindAsync(seeded.InitialAdminAuditLogId);
            Assert.NotNull(initialCaseAudit);
            Assert.NotNull(initialAdminAudit);

            var initialCaseMessage = initialCaseAudit!.Message;
            var initialCaseMetadata = initialCaseAudit.MetadataJson;
            var initialAdminMetadata = initialAdminAudit!.Metadata;

            var retention = await service.RunRetentionAsync(
                seeded.AdminUserId,
                "admin.security@teste.com",
                new AdminDisputeRetentionRunRequestDto(
                    RetentionDays: 180,
                    Take: 100,
                    DryRun: false));

            Assert.Equal(1, retention.Candidates);
            Assert.Equal(1, retention.AnonymizedCases);

            var reloadedCaseAudit = await context.ServiceDisputeCaseAuditEntries.FindAsync(seeded.InitialCaseAuditEntryId);
            var reloadedAdminAudit = await context.AdminAuditLogs.FindAsync(seeded.InitialAdminAuditLogId);
            Assert.NotNull(reloadedCaseAudit);
            Assert.NotNull(reloadedAdminAudit);

            Assert.Equal(initialCaseMessage, reloadedCaseAudit!.Message);
            Assert.Equal(initialCaseMetadata, reloadedCaseAudit.MetadataJson);
            Assert.Equal(initialAdminMetadata, reloadedAdminAudit!.Metadata);

            var retentionCaseAudit = await context.ServiceDisputeCaseAuditEntries
                .CountAsync(x => x.ServiceDisputeCaseId == seeded.DisputeCaseId && x.EventType == "dispute_lgpd_anonymized");
            var retentionAdminAudit = await context.AdminAuditLogs
                .CountAsync(x => x.Action == "DisputeLgpdRetentionRun");

            Assert.Equal(1, retentionCaseAudit);
            Assert.True(retentionAdminAudit >= 1);
        }
    }

    private static AdminDisputeQueueService BuildService(ConsertaPraMimDbContext context)
    {
        return new AdminDisputeQueueService(
            new ServiceDisputeCaseRepository(context),
            new UserRepository(context),
            new AdminAuditLogRepository(context),
            Mock.Of<IServicePaymentTransactionRepository>(),
            Mock.Of<IPaymentService>(),
            Mock.Of<IProviderCreditService>(),
            Mock.Of<INotificationService>());
    }

    private static async Task<SeededDisputeData> SeedOpenDisputeAsync(ConsertaPraMimDbContext context)
    {
        var admin = new User
        {
            Name = "Admin Security",
            Email = "admin.security@teste.com",
            PasswordHash = "hash",
            Phone = "11999999999",
            Role = UserRole.Admin
        };
        var client = new User
        {
            Name = "Cliente Security",
            Email = "cliente.security@teste.com",
            PasswordHash = "hash",
            Phone = "11888888888",
            Role = UserRole.Client
        };
        var provider = new User
        {
            Name = "Prestador Security",
            Email = "prestador.security@teste.com",
            PasswordHash = "hash",
            Phone = "11777777777",
            Role = UserRole.Provider
        };
        context.Users.AddRange(admin, client, provider);

        var request = new ServiceRequest
        {
            ClientId = client.Id,
            Category = ServiceCategory.Electrical,
            Status = ServiceRequestStatus.Scheduled,
            Description = "Teste seguranca trilha",
            AddressStreet = "Rua Seguranca 100",
            AddressCity = "Santos",
            AddressZip = "11704150",
            Latitude = -24.01,
            Longitude = -46.41
        };
        context.ServiceRequests.Add(request);

        var appointment = new ServiceAppointment
        {
            ServiceRequestId = request.Id,
            ClientId = client.Id,
            ProviderId = provider.Id,
            WindowStartUtc = DateTime.UtcNow.AddDays(1),
            WindowEndUtc = DateTime.UtcNow.AddDays(1).AddHours(1),
            Status = ServiceAppointmentStatus.Confirmed
        };
        context.ServiceAppointments.Add(appointment);

        var dispute = new ServiceDisputeCase
        {
            ServiceRequestId = request.Id,
            ServiceAppointmentId = appointment.Id,
            OpenedByUserId = client.Id,
            OpenedByRole = ServiceAppointmentActorRole.Client,
            CounterpartyUserId = provider.Id,
            CounterpartyRole = ServiceAppointmentActorRole.Provider,
            OwnedByAdminUserId = admin.Id,
            OwnedAtUtc = DateTime.UtcNow.AddMinutes(-50),
            Type = DisputeCaseType.ServiceQuality,
            Priority = DisputeCasePriority.High,
            Status = DisputeCaseStatus.Open,
            ReasonCode = "QUALITY",
            Description = "Cliente reportou problema",
            OpenedAtUtc = DateTime.UtcNow.AddHours(-3),
            SlaDueAtUtc = DateTime.UtcNow.AddHours(4),
            LastInteractionAtUtc = DateTime.UtcNow.AddHours(-2)
        };
        context.ServiceDisputeCases.Add(dispute);

        var initialCaseAudit = new ServiceDisputeCaseAuditEntry
        {
            ServiceDisputeCaseId = dispute.Id,
            ActorUserId = admin.Id,
            ActorRole = ServiceAppointmentActorRole.Admin,
            EventType = "dispute_case_viewed",
            Message = "Abertura da analise",
            MetadataJson = "{\"source\":\"security_seed\"}"
        };
        context.ServiceDisputeCaseAuditEntries.Add(initialCaseAudit);

        var initialAdminAudit = new AdminAuditLog
        {
            ActorUserId = admin.Id,
            ActorEmail = admin.Email,
            Action = "DisputeCaseViewed",
            TargetType = "ServiceDisputeCase",
            TargetId = dispute.Id,
            Metadata = "{\"source\":\"security_seed\"}"
        };
        context.AdminAuditLogs.Add(initialAdminAudit);

        await context.SaveChangesAsync();

        return new SeededDisputeData(
            admin.Id,
            dispute.Id,
            initialCaseAudit.Id,
            initialAdminAudit.Id);
    }

    private static async Task<SeededDisputeData> SeedClosedDisputeAsync(ConsertaPraMimDbContext context)
    {
        var seeded = await SeedOpenDisputeAsync(context);
        var dispute = await context.ServiceDisputeCases.FindAsync(seeded.DisputeCaseId);
        Assert.NotNull(dispute);

        dispute!.Status = DisputeCaseStatus.Resolved;
        dispute.OpenedAtUtc = DateTime.UtcNow.AddDays(-220);
        dispute.ClosedAtUtc = DateTime.UtcNow.AddDays(-200);
        dispute.LastInteractionAtUtc = DateTime.UtcNow.AddDays(-200);
        dispute.SlaDueAtUtc = DateTime.UtcNow.AddDays(-210);
        dispute.MetadataJson = "{\"outcome\":\"procedente\"}";
        dispute.ResolutionSummary = "Resolvido";
        await context.SaveChangesAsync();

        return seeded;
    }

    private sealed record SeededDisputeData(
        Guid AdminUserId,
        Guid DisputeCaseId,
        Guid InitialCaseAuditEntryId,
        Guid InitialAdminAuditLogId);
}
