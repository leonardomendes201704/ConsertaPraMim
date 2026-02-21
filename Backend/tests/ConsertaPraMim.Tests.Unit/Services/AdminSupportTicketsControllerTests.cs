using System.Security.Claims;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Controllers;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminSupportTicketsControllerTests
{
    /// <summary>
    /// Cenario: operador admin abre fila de chamados com filtros fora do intervalo permitido.
    /// Passos: executa Index com page/pageSize invalidos e valida os parametros enviados para APIs de usuarios e chamados.
    /// Resultado esperado: filtros normalizados (page=1, pageSize=100) e view carregada com fila + lista de atribuiveis.
    /// </summary>
    [Fact(DisplayName = "Admin support tickets controller | Index | Deve normalize filters e retornar queue com assignees")]
    public async Task Index_ShouldNormalizeFilters_AndReturnQueueWithAssignees()
    {
        var operationsClientMock = new Mock<IAdminOperationsApiClient>();
        var usersClientMock = new Mock<IAdminUsersApiClient>();

        var expectedResponse = new AdminSupportTicketListResponseDto(
            Items: Array.Empty<AdminSupportTicketSummaryDto>(),
            Page: 1,
            PageSize: 100,
            TotalCount: 0,
            TotalPages: 0,
            Indicators: new AdminSupportTicketQueueIndicatorsDto(0, 0, 0, 0, 0, 0, 0, 0));

        usersClientMock
            .Setup(client => client.GetUsersAsync(
                It.Is<AdminUsersFilterModel>(f =>
                    f.Role == "admin" &&
                    f.IsActive == true &&
                    f.Page == 1 &&
                    f.PageSize == 100),
                "api-token",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminApiResult<AdminUsersListResponseDto>.Ok(
                new AdminUsersListResponseDto(
                    1,
                    100,
                    1,
                    new[]
                    {
                        new AdminUserListItemDto(Guid.NewGuid(), "Admin", "admin@conserta.com", "", "Admin", true, DateTime.UtcNow)
                    })));

        operationsClientMock
            .Setup(client => client.GetSupportTicketsAsync(
                It.Is<AdminSupportTicketsFilterModel>(f =>
                    f.Status == "Open" &&
                    f.Priority == "High" &&
                    f.Page == 1 &&
                    f.PageSize == 100 &&
                    f.SortBy == "lastInteraction" &&
                    f.SortDescending),
                "api-token",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminApiResult<AdminSupportTicketListResponseDto>.Ok(expectedResponse));

        var controller = CreateController(operationsClientMock.Object, usersClientMock.Object);

        var result = await controller.Index(
            status: "Open",
            priority: "High",
            page: 0,
            pageSize: 500,
            sortBy: "lastInteraction",
            sortDescending: true);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminSupportTicketsIndexViewModel>(view.Model);
        Assert.Equal(1, model.Filters.Page);
        Assert.Equal(100, model.Filters.PageSize);
        Assert.Single(model.AdminAssignees);
        Assert.Same(expectedResponse, model.Tickets);

        operationsClientMock.VerifyAll();
        usersClientMock.VerifyAll();
    }

    /// <summary>
    /// Cenario: admin tenta responder chamado enviando mensagem vazia.
    /// Passos: chama AddMessage com texto em branco.
    /// Resultado esperado: redireciona para Details com erro de validacao e sem chamada ao backend.
    /// </summary>
    [Fact(DisplayName = "Admin support tickets controller | Add mensagem | Deve redirect com erro quando mensagem vazio")]
    public async Task AddMessage_ShouldRedirectWithError_WhenMessageIsEmpty()
    {
        var operationsClientMock = new Mock<IAdminOperationsApiClient>(MockBehavior.Strict);
        var usersClientMock = new Mock<IAdminUsersApiClient>(MockBehavior.Strict);
        var controller = CreateController(operationsClientMock.Object, usersClientMock.Object);
        var ticketId = Guid.NewGuid();

        var result = await controller.AddMessage(new AdminSupportTicketAddMessageWebRequest
        {
            TicketId = ticketId,
            Message = "   "
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(ticketId, redirect.RouteValues!["id"]);
        Assert.Equal("Mensagem obrigatoria.", controller.TempData["ErrorMessage"]);

        operationsClientMock.VerifyNoOtherCalls();
        usersClientMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Cenario: admin altera status do chamado para resolvido com nota operacional.
    /// Passos: mocka API de status com sucesso e executa UpdateStatus com dados do formulario.
    /// Resultado esperado: API e chamada com payload correto e usuario recebe mensagem de sucesso no redirect.
    /// </summary>
    [Fact(DisplayName = "Admin support tickets controller | Atualizar status | Deve call api e set sucesso mensagem")]
    public async Task UpdateStatus_ShouldCallApi_AndSetSuccessMessage()
    {
        var operationsClientMock = new Mock<IAdminOperationsApiClient>();
        var usersClientMock = new Mock<IAdminUsersApiClient>(MockBehavior.Strict);

        var ticketId = Guid.NewGuid();
        operationsClientMock
            .Setup(client => client.UpdateSupportTicketStatusAsync(
                ticketId,
                It.Is<AdminSupportTicketStatusUpdateRequestDto>(req => req.Status == "Resolved" && req.Note == "Concluido."),
                "api-token",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminApiResult<AdminSupportTicketDetailsDto>.Ok(BuildDetails(ticketId)));

        var controller = CreateController(operationsClientMock.Object, usersClientMock.Object);

        var result = await controller.UpdateStatus(new AdminSupportTicketStatusUpdateWebRequest
        {
            TicketId = ticketId,
            Status = "Resolved",
            Note = "Concluido."
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(ticketId, redirect.RouteValues!["id"]);
        Assert.Equal("Status atualizado com sucesso.", controller.TempData["SuccessMessage"]);

        operationsClientMock.VerifyAll();
        usersClientMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Cenario: atribuicao do chamado para outro admin falha na API de operacoes.
    /// Passos: mocka AssignSupportTicketAsync retornando falha e executa acao Assign.
    /// Resultado esperado: redireciona para Details preservando erro funcional retornado pelo backend.
    /// </summary>
    [Fact(DisplayName = "Admin support tickets controller | Assign | Deve redirect com erro quando api falha")]
    public async Task Assign_ShouldRedirectWithError_WhenApiFails()
    {
        var operationsClientMock = new Mock<IAdminOperationsApiClient>();
        var usersClientMock = new Mock<IAdminUsersApiClient>(MockBehavior.Strict);

        var ticketId = Guid.NewGuid();
        operationsClientMock
            .Setup(client => client.AssignSupportTicketAsync(
                ticketId,
                It.IsAny<AdminSupportTicketAssignRequestDto>(),
                "api-token",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminApiResult<AdminSupportTicketDetailsDto>.Fail("Falha de atribuicao."));

        var controller = CreateController(operationsClientMock.Object, usersClientMock.Object);

        var result = await controller.Assign(new AdminSupportTicketAssignWebRequest
        {
            TicketId = ticketId,
            AssignedAdminUserId = Guid.NewGuid(),
            Note = "Atribuir para suporte nivel 2"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(ticketId, redirect.RouteValues!["id"]);
        Assert.Equal("Falha de atribuicao.", controller.TempData["ErrorMessage"]);

        operationsClientMock.VerifyAll();
        usersClientMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Cenario: tela de atendimento faz polling para atualizar estado do chamado em tempo quase real.
    /// Passos: mocka detalhes com 1 mensagem e status Resolved; executa PollDetails.
    /// Resultado esperado: endpoint retorna success=true e snapshot consistente para refresh da UI.
    /// </summary>
    [Fact(DisplayName = "Admin support tickets controller | Poll details | Deve retornar snapshot quando api sucesso")]
    public async Task PollDetails_ShouldReturnSnapshot_WhenApiSucceeds()
    {
        var operationsClientMock = new Mock<IAdminOperationsApiClient>();
        var usersClientMock = new Mock<IAdminUsersApiClient>(MockBehavior.Strict);
        var ticketId = Guid.NewGuid();
        var details = BuildDetails(ticketId) with
        {
            Messages = new[]
            {
                new AdminSupportTicketMessageDto(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Admin",
                    "Admin",
                    "AdminReply",
                    "Resposta",
                    false,
                    null,
                    Array.Empty<SupportTicketAttachmentDto>(),
                    DateTime.UtcNow)
            }
        };

        operationsClientMock
            .Setup(client => client.GetSupportTicketDetailsAsync(
                ticketId,
                "api-token",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminApiResult<AdminSupportTicketDetailsDto>.Ok(details));

        var controller = CreateController(operationsClientMock.Object, usersClientMock.Object);
        var result = await controller.PollDetails(ticketId);

        var ok = Assert.IsType<OkObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        var snapshot = root.GetProperty("snapshot");
        Assert.Equal("Resolved", snapshot.GetProperty("status").GetString());
        Assert.Equal(1, snapshot.GetProperty("messageCount").GetInt32());
    }

    private static AdminSupportTicketsController CreateController(
        IAdminOperationsApiClient operationsApiClient,
        IAdminUsersApiClient usersApiClient)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(AdminClaimTypes.ApiToken, "api-token"),
                new Claim(ClaimTypes.Role, "Admin")
            }, "Tests"))
        };

        var controller = new AdminSupportTicketsController(operationsApiClient, usersApiClient)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
        };

        return controller;
    }

    private static AdminSupportTicketDetailsDto BuildDetails(Guid ticketId)
    {
        return new AdminSupportTicketDetailsDto(
            Ticket: new AdminSupportTicketSummaryDto(
                Id: ticketId,
                ProviderId: Guid.NewGuid(),
                ProviderName: "Prestador",
                ProviderEmail: "prestador@conserta.com",
                AssignedAdminUserId: null,
                AssignedAdminName: null,
                Subject: "Falha no pagamento",
                Category: "Pagamento",
                Priority: "High",
                Status: "Resolved",
                OpenedAtUtc: DateTime.UtcNow.AddHours(-4),
                LastInteractionAtUtc: DateTime.UtcNow,
                FirstAdminResponseAtUtc: DateTime.UtcNow.AddHours(-3),
                ClosedAtUtc: DateTime.UtcNow,
                MessageCount: 2,
                LastMessagePreview: "Finalizado",
                IsOverdueFirstResponse: false),
            MetadataJson: null,
            Messages: Array.Empty<AdminSupportTicketMessageDto>());
    }
}
