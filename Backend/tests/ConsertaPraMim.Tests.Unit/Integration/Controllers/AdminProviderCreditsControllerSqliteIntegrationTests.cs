using System.Security.Claims;
using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Hubs;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Infrastructure.Services;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integration.Controllers;

public class AdminProviderCreditsControllerSqliteIntegrationTests
{
    [Fact]
    public async Task Grant_ShouldPersistCredit_AndSendRealtimeNotification()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var provider = CreateProvider("prestador.credito.integracao@teste.com");
            var admin = CreateAdmin("admin.credito.integracao@teste.com");
            context.Users.AddRange(provider, admin);
            await context.SaveChangesAsync();

            var notificationHarness = CreateHubNotificationHarness();
            var controller = BuildController(
                context,
                notificationHarness.NotificationService,
                admin.Id,
                admin.Email);

            var grantResponse = await controller.Grant(new AdminProviderCreditGrantRequestDto(
                provider.Id,
                Amount: 55.50m,
                Reason: "Premio por excelencia operacional",
                GrantType: ProviderCreditGrantType.Premio,
                ExpiresAtUtc: DateTime.UtcNow.AddDays(20)));

            var ok = Assert.IsType<OkObjectResult>(grantResponse);
            var mutation = Assert.IsType<AdminProviderCreditMutationResultDto>(ok.Value);
            Assert.True(mutation.Success);
            Assert.True(mutation.NotificationSent);
            Assert.NotNull(mutation.CreditMutation);
            Assert.NotNull(mutation.CreditMutation!.Balance);
            Assert.Equal(55.50m, mutation.CreditMutation.Balance!.CurrentBalance);

            var balanceResponse = await controller.GetBalance(provider.Id, CancellationToken.None);
            var balanceOk = Assert.IsType<OkObjectResult>(balanceResponse);
            var balance = Assert.IsType<ProviderCreditBalanceDto>(balanceOk.Value);
            Assert.Equal(55.50m, balance.CurrentBalance);

            var statementResponse = await controller.GetStatement(
                provider.Id,
                fromUtc: null,
                toUtc: null,
                entryType: "Grant",
                page: 1,
                pageSize: 20,
                cancellationToken: CancellationToken.None);
            var statementOk = Assert.IsType<OkObjectResult>(statementResponse);
            var statement = Assert.IsType<ProviderCreditStatementDto>(statementOk.Value);
            Assert.Equal(1, statement.TotalCount);
            Assert.Single(statement.Items);
            Assert.Equal(ProviderCreditLedgerEntryType.Grant, statement.Items[0].EntryType);
            Assert.Equal(55.50m, statement.Items[0].Amount);

            var auditActions = await context.AdminAuditLogs
                .AsNoTracking()
                .Where(x =>
                    x.TargetType == "ProviderCreditWallet" &&
                    (x.Action == "ProviderCreditGrantCreated" || x.Action == "AdminProviderCreditGrantExecuted"))
                .Select(x => x.Action)
                .ToListAsync();

            Assert.Contains("ProviderCreditGrantCreated", auditActions);
            Assert.Contains("AdminProviderCreditGrantExecuted", auditActions);

            Assert.Contains(NotificationHub.BuildUserGroup(provider.Id), notificationHarness.GroupCalls);
            Assert.Single(notificationHarness.Payloads);
            var payload = notificationHarness.Payloads[0];
            Assert.Contains("premio", payload.Subject, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("/Profile", payload.ActionUrl);
            Assert.Contains("foram creditados", payload.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Reverse_ShouldReturnConflict_WhenBalanceIsInsufficient_AndShouldNotNotify()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var provider = CreateProvider("prestador.estorno.integracao@teste.com");
            var admin = CreateAdmin("admin.estorno.integracao@teste.com");
            context.Users.AddRange(provider, admin);
            await context.SaveChangesAsync();

            var notificationHarness = CreateHubNotificationHarness();
            var controller = BuildController(
                context,
                notificationHarness.NotificationService,
                admin.Id,
                admin.Email);

            var reverseResponse = await controller.Reverse(new AdminProviderCreditReversalRequestDto(
                provider.Id,
                Amount: 10m,
                Reason: "Estorno de ajuste sem saldo"));

            var conflict = Assert.IsType<ConflictObjectResult>(reverseResponse);
            var mutation = Assert.IsType<AdminProviderCreditMutationResultDto>(conflict.Value);
            Assert.False(mutation.Success);
            Assert.Equal("insufficient_balance", mutation.ErrorCode);
            Assert.False(mutation.NotificationSent);

            var reversalAuditCount = await context.AdminAuditLogs
                .AsNoTracking()
                .CountAsync(x => x.Action == "AdminProviderCreditReversalExecuted" && x.TargetId == provider.Id);
            Assert.Equal(0, reversalAuditCount);
            Assert.Empty(notificationHarness.GroupCalls);
            Assert.Empty(notificationHarness.Payloads);
        }
    }

    private static AdminProviderCreditsController BuildController(
        ConsertaPraMim.Infrastructure.Data.ConsertaPraMimDbContext context,
        INotificationService notificationService,
        Guid actorUserId,
        string actorEmail)
    {
        var userRepository = new UserRepository(context);
        var adminAuditRepository = new AdminAuditLogRepository(context);
        var providerCreditRepository = new ProviderCreditRepository(context);

        var providerCreditService = new ProviderCreditService(
            providerCreditRepository,
            userRepository,
            adminAuditRepository,
            NullLogger<ProviderCreditService>.Instance);

        var adminProviderCreditService = new AdminProviderCreditService(
            userRepository,
            providerCreditService,
            notificationService,
            adminAuditRepository,
            NullLogger<AdminProviderCreditService>.Instance);

        return new AdminProviderCreditsController(adminProviderCreditService, providerCreditService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, actorUserId.ToString("D")),
                        new Claim(ClaimTypes.Email, actorEmail),
                        new Claim(ClaimTypes.Role, UserRole.Admin.ToString())
                    }))
                }
            }
        };
    }

    private static NotificationHarness CreateHubNotificationHarness()
    {
        var payloads = new List<NotificationPayload>();
        var groupCalls = new List<string>();

        var proxyMock = new Mock<IClientProxy>();
        proxyMock
            .Setup(proxy => proxy.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((method, args, _) =>
            {
                if (!string.Equals(method, "ReceiveNotification", StringComparison.Ordinal))
                {
                    return;
                }

                var body = args.Length > 0 ? args[0] : null;
                payloads.Add(new NotificationPayload(
                    ReadProperty(body, "subject") ?? string.Empty,
                    ReadProperty(body, "message") ?? string.Empty,
                    ReadProperty(body, "actionUrl")));
            })
            .Returns(Task.CompletedTask);

        var clientsMock = new Mock<IHubClients>();
        clientsMock
            .Setup(clients => clients.Group(It.IsAny<string>()))
            .Returns((string groupName) =>
            {
                groupCalls.Add(groupName);
                return proxyMock.Object;
            });

        var hubContextMock = new Mock<IHubContext<NotificationHub>>();
        hubContextMock.SetupGet(hub => hub.Clients).Returns(clientsMock.Object);

        var notificationService = new HubNotificationService(
            Mock.Of<ILogger<HubNotificationService>>(),
            hubContextMock.Object);

        return new NotificationHarness(notificationService, payloads, groupCalls);
    }

    private static string? ReadProperty(object? source, string propertyName)
    {
        if (source == null)
        {
            return null;
        }

        var property = source.GetType().GetProperty(propertyName);
        return property?.GetValue(source)?.ToString();
    }

    private static User CreateProvider(string email)
    {
        return new User
        {
            Name = "Prestador Integracao Creditos",
            Email = email,
            PasswordHash = "hash",
            Phone = "11988887777",
            Role = UserRole.Provider,
            IsActive = true
        };
    }

    private static User CreateAdmin(string email)
    {
        return new User
        {
            Name = "Admin Integracao Creditos",
            Email = email,
            PasswordHash = "hash",
            Phone = "11977776666",
            Role = UserRole.Admin,
            IsActive = true
        };
    }

    private sealed record NotificationPayload(string Subject, string Message, string? ActionUrl);

    private sealed record NotificationHarness(
        INotificationService NotificationService,
        List<NotificationPayload> Payloads,
        List<string> GroupCalls);
}
