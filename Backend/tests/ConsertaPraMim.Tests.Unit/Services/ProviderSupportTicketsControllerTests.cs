using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Provider.Controllers;
using ConsertaPraMim.Web.Provider.Models;
using ConsertaPraMim.Web.Provider.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using System.Text.Json;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ProviderSupportTicketsControllerTests
{
    /// <summary>
    /// Cenario: prestador consulta lista de chamados com filtros e paginacao fora dos limites aceitos.
    /// Passos: chama Index com page=0/pageSize=500 e verifica argumentos enviados para o backend de suporte.
    /// Resultado esperado: filtros normalizados e view carregada com resposta retornada pela API.
    /// </summary>
    [Fact(DisplayName = "Prestador support tickets controller | Index | Deve normalize filters e retornar view")]
    public async Task Index_ShouldNormalizeFiltersAndReturnView()
    {
        var backendApiClientMock = new Mock<IProviderBackendApiClient>();
        var expectedResponse = new MobileProviderSupportTicketListResponseDto(
            Items: Array.Empty<MobileProviderSupportTicketSummaryDto>(),
            Page: 1,
            PageSize: 100,
            TotalCount: 0,
            TotalPages: 0);

        backendApiClientMock
            .Setup(client => client.GetSupportTicketsAsync(
                "Open",
                "High",
                "erro",
                1,
                100,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((expectedResponse, null as string));

        var controller = CreateController(backendApiClientMock.Object);

        var result = await controller.Index(new SupportTicketFiltersViewModel
        {
            Status = "Open",
            Priority = "High",
            Search = "erro",
            Page = 0,
            PageSize = 500
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SupportTicketsIndexViewModel>(view.Model);
        Assert.Equal(1, model.Filters.Page);
        Assert.Equal(100, model.Filters.PageSize);
        Assert.Same(expectedResponse, model.Response);

        backendApiClientMock.VerifyAll();
    }

    /// <summary>
    /// Cenario: formulario de abertura de chamado chega invalido (ModelState com erro).
    /// Passos: adiciona erro de validacao no controller e executa Create com modelo incompleto.
    /// Resultado esperado: retorna a propria view com o mesmo modelo, sem acionar API externa.
    /// </summary>
    [Fact(DisplayName = "Prestador support tickets controller | Criar | Deve retornar view quando model state invalido")]
    public async Task Create_ShouldReturnView_WhenModelStateIsInvalid()
    {
        var backendApiClientMock = new Mock<IProviderBackendApiClient>(MockBehavior.Strict);
        var controller = CreateController(backendApiClientMock.Object);
        controller.ModelState.AddModelError("Subject", "required");

        var model = new SupportTicketCreateViewModel
        {
            Subject = string.Empty,
            InitialMessage = string.Empty
        };

        var result = await controller.Create(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        backendApiClientMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Cenario: prestador abre chamado com sucesso.
    /// Passos: mocka CreateSupportTicketAsync retornando ticket criado e submete formulario valido.
    /// Resultado esperado: redirect para Details do novo chamado e mensagem de sucesso ao usuario.
    /// </summary>
    [Fact(DisplayName = "Prestador support tickets controller | Criar | Deve redirect para details quando ticket criado")]
    public async Task Create_ShouldRedirectToDetails_WhenTicketIsCreated()
    {
        var backendApiClientMock = new Mock<IProviderBackendApiClient>();
        var details = BuildTicketDetails();

        backendApiClientMock
            .Setup(client => client.CreateSupportTicketAsync(
                It.IsAny<MobileProviderCreateSupportTicketRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((details, null as string));

        var controller = CreateController(backendApiClientMock.Object);
        var model = new SupportTicketCreateViewModel
        {
            Subject = "Falha no pagamento",
            Category = "Pagamento",
            Priority = 3,
            InitialMessage = "Erro ao processar o pagamento."
        };

        var result = await controller.Create(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(details.Ticket.Id, redirect.RouteValues!["id"]);
        Assert.Equal("Chamado aberto com sucesso.", controller.TempData["Success"]);

        backendApiClientMock.Verify(
            client => client.CreateSupportTicketAsync(
                It.IsAny<MobileProviderCreateSupportTicketRequestDto>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Cenario: prestador tenta adicionar resposta ao chamado sem texto.
    /// Passos: executa AddMessage com conteudo em branco.
    /// Resultado esperado: redireciona para Details com erro de validacao e sem chamada ao backend.
    /// </summary>
    [Fact(DisplayName = "Prestador support tickets controller | Add mensagem | Deve redirect com erro quando mensagem vazio")]
    public async Task AddMessage_ShouldRedirectWithError_WhenMessageIsEmpty()
    {
        var backendApiClientMock = new Mock<IProviderBackendApiClient>(MockBehavior.Strict);
        var controller = CreateController(backendApiClientMock.Object);
        var ticketId = Guid.NewGuid();

        var result = await controller.AddMessage(ticketId, "   ");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(ticketId, redirect.RouteValues!["id"]);
        Assert.Equal("Mensagem obrigatoria.", controller.TempData["Error"]);

        backendApiClientMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Cenario: acao de fechamento acionada com identificador de chamado invalido.
    /// Passos: chama Close com Guid.Empty.
    /// Resultado esperado: redireciona para Index com alerta de chamado invalido e bloqueio da operacao.
    /// </summary>
    [Fact(DisplayName = "Prestador support tickets controller | Fechar | Deve redirect para index quando ticket id invalido")]
    public async Task Close_ShouldRedirectToIndex_WhenTicketIdIsInvalid()
    {
        var backendApiClientMock = new Mock<IProviderBackendApiClient>(MockBehavior.Strict);
        var controller = CreateController(backendApiClientMock.Object);

        var result = await controller.Close(Guid.Empty);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Chamado invalido.", controller.TempData["Error"]);

        backendApiClientMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Cenario: pagina de detalhes do prestador realiza polling para sincronizar estado do chamado.
    /// Passos: mocka detalhe existente e executa PollDetails para o ticket informado.
    /// Resultado esperado: resposta JSON com success=true e snapshot de status/mensagens para atualizar a UI.
    /// </summary>
    [Fact(DisplayName = "Prestador support tickets controller | Poll details | Deve retornar snapshot quando ticket existe")]
    public async Task PollDetails_ShouldReturnSnapshot_WhenTicketExists()
    {
        var backendApiClientMock = new Mock<IProviderBackendApiClient>();
        var details = BuildTicketDetails();

        backendApiClientMock
            .Setup(client => client.GetSupportTicketDetailsAsync(
                details.Ticket.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((details, null as string));

        var controller = CreateController(backendApiClientMock.Object);
        var result = await controller.PollDetails(details.Ticket.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        var snapshot = root.GetProperty("snapshot");
        Assert.Equal("Open", snapshot.GetProperty("status").GetString());
        Assert.Equal(1, snapshot.GetProperty("messageCount").GetInt32());
    }

    private static SupportTicketsController CreateController(IProviderBackendApiClient backendApiClient)
    {
        var controller = new SupportTicketsController(backendApiClient)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.TempData = new TempDataDictionary(
            controller.ControllerContext.HttpContext,
            Mock.Of<ITempDataProvider>());

        return controller;
    }

    private static MobileProviderSupportTicketDetailsDto BuildTicketDetails()
    {
        var ticketId = Guid.NewGuid();

        return new MobileProviderSupportTicketDetailsDto(
            Ticket: new MobileProviderSupportTicketSummaryDto(
                Id: ticketId,
                Subject: "Falha no pagamento",
                Category: "Pagamento",
                Priority: "High",
                Status: "Open",
                OpenedAtUtc: DateTime.UtcNow,
                LastInteractionAtUtc: DateTime.UtcNow,
                ClosedAtUtc: null,
                AssignedAdminUserId: null,
                AssignedAdminName: null,
                MessageCount: 1,
                LastMessagePreview: "Erro ao processar o pagamento."),
            FirstAdminResponseAtUtc: null,
            Messages: new List<MobileProviderSupportTicketMessageDto>
            {
                new(
                    Id: Guid.NewGuid(),
                    AuthorUserId: Guid.NewGuid(),
                    AuthorRole: "Provider",
                    AuthorName: "Prestador",
                    MessageType: "ProviderOpened",
                    MessageText: "Erro ao processar o pagamento.",
                    Attachments: Array.Empty<SupportTicketAttachmentDto>(),
                    CreatedAtUtc: DateTime.UtcNow)
            });
    }
}
