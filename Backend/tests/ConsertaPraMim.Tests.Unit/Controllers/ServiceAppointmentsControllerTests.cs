using System.Security.Claims;
using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class ServiceAppointmentsControllerTests
{
    /// <summary>
    /// Cenario: o endpoint de consulta de slots eh acionado sem identidade valida do usuario autenticado.
    /// Passos: o teste remove o claim de NameIdentifier e executa a acao de slots.
    /// Resultado esperado: o controller responde Unauthorized, sem encaminhar a requisicao para a camada de servico.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Obter slots | Deve retornar nao autorizado quando name identifier missing")]
    public async Task GetSlots_ShouldReturnUnauthorized_WhenNameIdentifierIsMissing()
    {
        var serviceMock = new Mock<IServiceAppointmentService>();
        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetSlots(new GetServiceAppointmentSlotsQueryDto(
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(4)));

        Assert.IsType<UnauthorizedResult>(result);
    }

    /// <summary>
    /// Cenario: o cliente tenta criar agendamento em horario indisponivel.
    /// Passos: o teste configura o servico para retornar erro de conflito por slot nao disponivel.
    /// Resultado esperado: o controller traduz a resposta para HTTP 409 Conflict.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Criar | Deve retornar conflito quando slot unavailable")]
    public async Task Create_ShouldReturnConflict_WhenSlotIsUnavailable()
    {
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CreateServiceAppointmentRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "slot_unavailable",
                ErrorMessage: "Janela indisponivel."));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Client.ToString());

        var result = await controller.Create(new CreateServiceAppointmentRequestDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(3)));

        Assert.IsType<ConflictObjectResult>(result);
    }

    /// <summary>
    /// Cenario: o usuario autenticado consulta a propria lista de agendamentos.
    /// Passos: o teste simula retorno com itens no servico de appointments.
    /// Resultado esperado: a acao responde 200 OK com payload contendo os agendamentos do ator.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Obter mine | Deve retornar ok com appointments")]
    public async Task GetMine_ShouldReturnOkWithAppointments()
    {
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.GetMyAppointmentsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<ServiceAppointmentDto>
            {
                new(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    ServiceAppointmentStatus.PendingProviderConfirmation.ToString(),
                    DateTime.UtcNow.AddHours(4),
                    DateTime.UtcNow.AddHours(5),
                    DateTime.UtcNow.AddHours(1),
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    DateTime.UtcNow,
                    null,
                    Array.Empty<ServiceAppointmentHistoryDto>())
            });

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());

        var result = await controller.GetMine(null, null);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ServiceAppointmentDto>>(ok.Value);
        Assert.Single(list);
    }

    /// <summary>
    /// Cenario: o usuario solicita detalhes de um agendamento existente.
    /// Passos: o teste injeta retorno valido do servico para o id informado.
    /// Resultado esperado: o controller devolve 200 OK com o detalhe do appointment.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Obter por id | Deve retornar ok quando appointment existe")]
    public async Task GetById_ShouldReturnOk_WhenAppointmentExists()
    {
        var appointment = new ServiceAppointmentDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ServiceAppointmentStatus.PendingProviderConfirmation.ToString(),
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(3),
            DateTime.UtcNow.AddHours(1),
            "Teste",
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            null,
            Array.Empty<ServiceAppointmentHistoryDto>());

        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<string>(), appointment.Id))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(true, appointment));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Client.ToString());

        var result = await controller.GetById(appointment.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ServiceAppointmentDto>(ok.Value);
        Assert.Equal(appointment.Id, dto.Id);
    }

    /// <summary>
    /// Cenario: a confirmacao eh solicitada em estado de negocio que nao permite a transicao.
    /// Passos: o teste simula resposta de estado invalido no servico de confirmacao.
    /// Resultado esperado: o endpoint responde Conflict para refletir violacao de fluxo.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Confirm | Deve retornar conflito quando servico returns invalido state")]
    public async Task Confirm_ShouldReturnConflict_WhenServiceReturnsInvalidState()
    {
        var appointmentId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.ConfirmAsync(It.IsAny<Guid>(), It.IsAny<string>(), appointmentId))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "invalid_state",
                ErrorMessage: "Status invalido."));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());

        var result = await controller.Confirm(appointmentId);

        Assert.IsType<ConflictObjectResult>(result);
    }

    /// <summary>
    /// Cenario: o agendamento pendente eh rejeitado com sucesso pelo ator autorizado.
    /// Passos: o teste configura o servico para concluir rejeicao sem erros.
    /// Resultado esperado: o controller retorna 200 OK com resultado de rejeicao aplicado.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Reject | Deve retornar ok quando servico rejects successfully")]
    public async Task Reject_ShouldReturnOk_WhenServiceRejectsSuccessfully()
    {
        var appointment = new ServiceAppointmentDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ServiceAppointmentStatus.RejectedByProvider.ToString(),
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(3),
            null,
            "Nao tenho disponibilidade",
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            null,
            Array.Empty<ServiceAppointmentHistoryDto>());

        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.RejectAsync(It.IsAny<Guid>(), It.IsAny<string>(), appointment.Id, It.IsAny<RejectServiceAppointmentRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(true, appointment));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());

        var result = await controller.Reject(appointment.Id, new RejectServiceAppointmentRequestDto("Nao tenho disponibilidade"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ServiceAppointmentDto>(ok.Value);
        Assert.Equal(ServiceAppointmentStatus.RejectedByProvider.ToString(), dto.Status);
    }

    /// <summary>
    /// Cenario: o usuario solicita reagendamento dentro das regras permitidas.
    /// Passos: o teste executa a acao com retorno de sucesso no servico.
    /// Resultado esperado: a resposta HTTP eh 200 OK indicando requisicao de reagendamento registrada.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Requisicao reschedule | Deve retornar ok quando servico sucesso")]
    public async Task RequestReschedule_ShouldReturnOk_WhenServiceSucceeds()
    {
        var appointment = new ServiceAppointmentDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ServiceAppointmentStatus.RescheduleRequestedByClient.ToString(),
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(1),
            null,
            "Reagendamento solicitado",
            DateTime.UtcNow.AddDays(1).AddHours(2),
            DateTime.UtcNow.AddDays(1).AddHours(3),
            DateTime.UtcNow,
            UserRole.Client.ToString(),
            "Compromisso pessoal",
            DateTime.UtcNow,
            DateTime.UtcNow,
            Array.Empty<ServiceAppointmentHistoryDto>());

        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.RequestRescheduleAsync(It.IsAny<Guid>(), It.IsAny<string>(), appointment.Id, It.IsAny<RequestServiceAppointmentRescheduleDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(true, appointment));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Client.ToString());

        var result = await controller.RequestReschedule(
            appointment.Id,
            new RequestServiceAppointmentRescheduleDto(
                DateTime.UtcNow.AddDays(1).AddHours(2),
                DateTime.UtcNow.AddDays(1).AddHours(3),
                "Compromisso pessoal"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ServiceAppointmentDto>(ok.Value);
        Assert.Equal(ServiceAppointmentStatus.RescheduleRequestedByClient.ToString(), dto.Status);
    }

    /// <summary>
    /// Cenario: o cancelamento viola politica temporal ou operacional definida para o agendamento.
    /// Passos: o teste injeta retorno de politica violada na camada de dominio.
    /// Resultado esperado: o controller converte para 409 Conflict.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Cancelar | Deve retornar conflito quando politica violated")]
    public async Task Cancel_ShouldReturnConflict_WhenPolicyIsViolated()
    {
        var appointmentId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.CancelAsync(It.IsAny<Guid>(), It.IsAny<string>(), appointmentId, It.IsAny<CancelServiceAppointmentRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "policy_violation",
                ErrorMessage: "Antecedencia insuficiente."));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Client.ToString());

        var result = await controller.Cancel(appointmentId, new CancelServiceAppointmentRequestDto("Nao estarei em casa"));

        Assert.IsType<ConflictObjectResult>(result);
    }

    /// <summary>
    /// Cenario: o prestador tenta registrar chegada duplicada no mesmo atendimento.
    /// Passos: o teste simula erro de duplicate checkin vindo do servico.
    /// Resultado esperado: a acao responde Conflict para impedir duplicidade de chegada.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Marcar arrived | Deve retornar conflito quando servico returns duplicate checkin")]
    public async Task MarkArrived_ShouldReturnConflict_WhenServiceReturnsDuplicateCheckin()
    {
        var appointmentId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.MarkArrivedAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                It.IsAny<MarkServiceAppointmentArrivalRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "duplicate_checkin",
                ErrorMessage: "Chegada ja registrada."));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());

        var result = await controller.MarkArrived(
            appointmentId,
            new MarkServiceAppointmentArrivalRequestDto(-24.01, -46.41, 10));

        Assert.IsType<ConflictObjectResult>(result);
    }

    /// <summary>
    /// Cenario: a execucao do atendimento eh iniciada apos pre-condicoes validas.
    /// Passos: o teste chama start execution com retorno de sucesso do servico.
    /// Resultado esperado: o endpoint retorna 200 OK confirmando inicio da execucao.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Start execution | Deve retornar ok quando servico sucesso")]
    public async Task StartExecution_ShouldReturnOk_WhenServiceSucceeds()
    {
        var appointment = new ServiceAppointmentDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ServiceAppointmentStatus.InProgress.ToString(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(2),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            Array.Empty<ServiceAppointmentHistoryDto>(),
            DateTime.UtcNow.AddMinutes(-5),
            -24.01,
            -46.41,
            8.0,
            null,
            DateTime.UtcNow);

        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.StartExecutionAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointment.Id,
                It.IsAny<StartServiceAppointmentExecutionRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(true, appointment));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());

        var result = await controller.StartExecution(
            appointment.Id,
            new StartServiceAppointmentExecutionRequestDto("Inicio"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ServiceAppointmentDto>(ok.Value);
        Assert.Equal(ServiceAppointmentStatus.InProgress.ToString(), dto.Status);
    }

    /// <summary>
    /// Cenario: cliente ou prestador responde confirmacao de presenca no agendamento.
    /// Passos: o teste prepara resposta valida e servico bem sucedido para a operacao.
    /// Resultado esperado: o controller retorna 200 OK com confirmacao processada.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Respond presence | Deve retornar ok quando servico sucesso")]
    public async Task RespondPresence_ShouldReturnOk_WhenServiceSucceeds()
    {
        var appointmentId = Guid.NewGuid();
        var appointment = new ServiceAppointmentDto(
            appointmentId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ServiceAppointmentStatus.Confirmed.ToString(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(2),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            Array.Empty<ServiceAppointmentHistoryDto>(),
            ClientPresenceConfirmed: true,
            ClientPresenceRespondedAtUtc: DateTime.UtcNow);

        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.RespondPresenceAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                It.IsAny<RespondServiceAppointmentPresenceRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(true, appointment));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Client.ToString());

        var result = await controller.RespondPresence(
            appointmentId,
            new RespondServiceAppointmentPresenceRequestDto(true, "Confirmado"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ServiceAppointmentDto>(ok.Value);
        Assert.Equal(appointmentId, dto.Id);
        Assert.True(dto.ClientPresenceConfirmed);
    }

    /// <summary>
    /// Cenario: o status operacional recebe transicao proibida pela maquina de estados do atendimento.
    /// Passos: o teste simula tentativa invalida de transicao no servico.
    /// Resultado esperado: a API devolve 409 Conflict para sinalizar regra de negocio infringida.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Atualizar operational status | Deve retornar conflito quando transition invalido")]
    public async Task UpdateOperationalStatus_ShouldReturnConflict_WhenTransitionIsInvalid()
    {
        var appointmentId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.UpdateOperationalStatusAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                It.IsAny<UpdateServiceAppointmentOperationalStatusRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "invalid_operational_transition",
                ErrorMessage: "Transicao invalida."));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());

        var result = await controller.UpdateOperationalStatus(
            appointmentId,
            new UpdateServiceAppointmentOperationalStatusRequestDto("InService", "Teste"));

        Assert.IsType<ConflictObjectResult>(result);
    }

    /// <summary>
    /// Cenario: o agendamento possui checklist operacional previamente cadastrado.
    /// Passos: o teste solicita o checklist por id com retorno existente no servico.
    /// Resultado esperado: o controller responde 200 OK entregando os itens do checklist.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Obter checklist | Deve retornar ok quando checklist existe")]
    public async Task GetChecklist_ShouldReturnOk_WhenChecklistExists()
    {
        var appointmentId = Guid.NewGuid();
        var checklistServiceMock = new Mock<IServiceAppointmentChecklistService>();
        checklistServiceMock
            .Setup(s => s.GetChecklistAsync(It.IsAny<Guid>(), It.IsAny<string>(), appointmentId))
            .ReturnsAsync(new ServiceAppointmentChecklistResultDto(
                true,
                new ServiceAppointmentChecklistDto(
                    appointmentId,
                    Guid.NewGuid(),
                    "Eletrica - padrao",
                    "Eletrica",
                    true,
                    2,
                    1,
                    Array.Empty<ServiceChecklistItemDto>(),
                    Array.Empty<ServiceChecklistHistoryDto>())));

        var controller = CreateController(
            Mock.Of<IServiceAppointmentService>(),
            Guid.NewGuid(),
            UserRole.Provider.ToString(),
            checklistServiceMock.Object);

        var result = await controller.GetChecklist(appointmentId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ServiceAppointmentChecklistDto>(ok.Value);
        Assert.Equal(appointmentId, dto.AppointmentId);
    }

    /// <summary>
    /// Cenario: tentativa de salvar item de checklist sem evidencia obrigatoria.
    /// Passos: o teste executa upsert com retorno de conflito por evidence required.
    /// Resultado esperado: a acao retorna 409 Conflict, preservando a regra de conclusao.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Upsert checklist item | Deve retornar conflito quando evidence required")]
    public async Task UpsertChecklistItem_ShouldReturnConflict_WhenEvidenceIsRequired()
    {
        var appointmentId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var checklistServiceMock = new Mock<IServiceAppointmentChecklistService>();
        checklistServiceMock
            .Setup(s => s.UpsertItemResponseAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                It.IsAny<UpsertServiceChecklistItemResponseRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentChecklistResultDto(
                false,
                ErrorCode: "evidence_required",
                ErrorMessage: "Item exige evidencia."));

        var controller = CreateController(
            Mock.Of<IServiceAppointmentService>(),
            Guid.NewGuid(),
            UserRole.Provider.ToString(),
            checklistServiceMock.Object);

        var result = await controller.UpsertChecklistItem(
            appointmentId,
            itemId,
            new UpsertServiceChecklistItemResponseRequestDto(
                itemId,
                true,
                "Teste",
                null,
                null,
                null,
                null,
                false));

        Assert.IsType<ConflictObjectResult>(result);
    }

    /// <summary>
    /// Cenario: o cliente aprova alteracao de escopo pendente de decisao.
    /// Passos: o teste chama approve scope change com fluxo de servico bem sucedido.
    /// Resultado esperado: o endpoint retorna 200 OK com aprovacao efetivada.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Approve scope change | Deve retornar ok quando servico sucesso")]
    public async Task ApproveScopeChange_ShouldReturnOk_WhenServiceSucceeds()
    {
        var appointmentId = Guid.NewGuid();
        var scopeChangeId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.ApproveScopeChangeRequestAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                scopeChangeId))
            .ReturnsAsync(new ServiceScopeChangeRequestOperationResultDto(
                true,
                new ServiceScopeChangeRequestDto(
                    scopeChangeId,
                    Guid.NewGuid(),
                    appointmentId,
                    Guid.NewGuid(),
                    1,
                    ServiceScopeChangeRequestStatus.ApprovedByClient.ToString(),
                    "Escopo extra",
                    "Detalhes",
                    99.90m,
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    null,
                    null,
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    Array.Empty<ServiceScopeChangeAttachmentDto>())));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Client.ToString());
        var result = await controller.ApproveScopeChange(appointmentId, scopeChangeId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ServiceScopeChangeRequestDto>(ok.Value);
        Assert.Equal(ServiceScopeChangeRequestStatus.ApprovedByClient.ToString(), dto.Status);
    }

    /// <summary>
    /// Cenario: rejeicao de mudanca de escopo eh tentada em estado nao elegivel.
    /// Passos: o teste injeta resposta de estado invalido ao chamar reject scope change.
    /// Resultado esperado: o controller retorna Conflict para refletir inconsistencias de estado.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Reject scope change | Deve retornar conflito quando state invalido")]
    public async Task RejectScopeChange_ShouldReturnConflict_WhenStateIsInvalid()
    {
        var appointmentId = Guid.NewGuid();
        var scopeChangeId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.RejectScopeChangeRequestAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                scopeChangeId,
                It.IsAny<RejectServiceScopeChangeRequestDto>()))
            .ReturnsAsync(new ServiceScopeChangeRequestOperationResultDto(
                false,
                ErrorCode: "invalid_state",
                ErrorMessage: "Aditivo ja respondido."));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Client.ToString());
        var result = await controller.RejectScopeChange(
            appointmentId,
            scopeChangeId,
            new RejectServiceScopeChangeRequestDto("Nao concordo"));

        Assert.IsType<ConflictObjectResult>(result);
    }

    /// <summary>
    /// Cenario: o cliente abre reclamacao de garantia dentro das condicoes permitidas.
    /// Passos: o teste executa criacao de warranty claim com retorno positivo do servico.
    /// Resultado esperado: a API responde 200 OK confirmando abertura do chamado de garantia.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Criar warranty claim | Deve retornar ok quando servico sucesso")]
    public async Task CreateWarrantyClaim_ShouldReturnOk_WhenServiceSucceeds()
    {
        var appointmentId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.CreateWarrantyClaimAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                It.IsAny<CreateServiceWarrantyClaimRequestDto>()))
            .ReturnsAsync(new ServiceWarrantyClaimOperationResultDto(
                true,
                new ServiceWarrantyClaimDto(
                    claimId,
                    Guid.NewGuid(),
                    appointmentId,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    ServiceWarrantyClaimStatus.PendingProviderReview.ToString(),
                    "Motor da bomba voltou a falhar.",
                    null,
                    null,
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddDays(30),
                    DateTime.UtcNow.AddHours(48),
                    null,
                    null,
                    null,
                    DateTime.UtcNow,
                    null)));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Client.ToString());
        var result = await controller.CreateWarrantyClaim(
            appointmentId,
            new CreateServiceWarrantyClaimRequestDto("Motor da bomba voltou a falhar."));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ServiceWarrantyClaimDto>(ok.Value);
        Assert.Equal(claimId, dto.Id);
        Assert.Equal(ServiceWarrantyClaimStatus.PendingProviderReview.ToString(), dto.Status);
    }

    /// <summary>
    /// Cenario: tentativa de abrir garantia apos expiracao da janela de cobertura.
    /// Passos: o teste simula erro de claim expired vindo do servico de appointments.
    /// Resultado esperado: o controller converte o resultado para HTTP 409 Conflict.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Criar warranty claim | Deve retornar conflito quando claim expired")]
    public async Task CreateWarrantyClaim_ShouldReturnConflict_WhenClaimIsExpired()
    {
        var appointmentId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.CreateWarrantyClaimAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                It.IsAny<CreateServiceWarrantyClaimRequestDto>()))
            .ReturnsAsync(new ServiceWarrantyClaimOperationResultDto(
                false,
                ErrorCode: "warranty_expired",
                ErrorMessage: "Prazo expirado."));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Client.ToString());
        var result = await controller.CreateWarrantyClaim(
            appointmentId,
            new CreateServiceWarrantyClaimRequestDto("Falha intermitente."));

        Assert.IsType<ConflictObjectResult>(result);
    }

    /// <summary>
    /// Cenario: o prestador responde uma reclamacao de garantia com dados validos.
    /// Passos: o teste aciona o endpoint de resposta com fluxo aprovado na aplicacao.
    /// Resultado esperado: o retorno eh 200 OK com processamento da resposta de garantia.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Respond warranty claim | Deve retornar ok quando servico sucesso")]
    public async Task RespondWarrantyClaim_ShouldReturnOk_WhenServiceSucceeds()
    {
        var appointmentId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.RespondWarrantyClaimAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                claimId,
                It.IsAny<RespondServiceWarrantyClaimRequestDto>()))
            .ReturnsAsync(new ServiceWarrantyClaimOperationResultDto(
                true,
                new ServiceWarrantyClaimDto(
                    claimId,
                    Guid.NewGuid(),
                    appointmentId,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    ServiceWarrantyClaimStatus.AcceptedByProvider.ToString(),
                    "Falha intermitente",
                    "Aceito",
                    null,
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddDays(30),
                    DateTime.UtcNow.AddHours(48),
                    DateTime.UtcNow,
                    null,
                    null,
                    DateTime.UtcNow,
                    DateTime.UtcNow)));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());
        var result = await controller.RespondWarrantyClaim(
            appointmentId,
            claimId,
            new RespondServiceWarrantyClaimRequestDto(true, "Vamos agendar revisita"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ServiceWarrantyClaimDto>(ok.Value);
        Assert.Equal(ServiceWarrantyClaimStatus.AcceptedByProvider.ToString(), dto.Status);
    }

    /// <summary>
    /// Cenario: a resposta da garantia chega com motivo invalido para a decisao selecionada.
    /// Passos: o teste configura validacao de dominio para rejeitar reason inconsistente.
    /// Resultado esperado: a acao retorna 400 BadRequest.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Respond warranty claim | Deve retornar invalida requisicao quando reason invalido")]
    public async Task RespondWarrantyClaim_ShouldReturnBadRequest_WhenReasonIsInvalid()
    {
        var appointmentId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.RespondWarrantyClaimAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                claimId,
                It.IsAny<RespondServiceWarrantyClaimRequestDto>()))
            .ReturnsAsync(new ServiceWarrantyClaimOperationResultDto(
                false,
                ErrorCode: "invalid_warranty_response_reason",
                ErrorMessage: "Motivo obrigatorio."));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());
        var result = await controller.RespondWarrantyClaim(
            appointmentId,
            claimId,
            new RespondServiceWarrantyClaimRequestDto(false, null));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Cenario: uma revisitacao de garantia eh agendada em slot elegivel.
    /// Passos: o teste chama schedule warranty revisit com dados validos e sucesso no servico.
    /// Resultado esperado: o endpoint devolve 200 OK com novo agendamento de revisita.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Agendar warranty revisit | Deve retornar ok quando servico sucesso")]
    public async Task ScheduleWarrantyRevisit_ShouldReturnOk_WhenServiceSucceeds()
    {
        var appointmentId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var revisitAppointmentId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.ScheduleWarrantyRevisitAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                claimId,
                It.IsAny<ScheduleServiceWarrantyRevisitRequestDto>()))
            .ReturnsAsync(new ServiceWarrantyRevisitOperationResultDto(
                true,
                new ServiceWarrantyClaimDto(
                    claimId,
                    Guid.NewGuid(),
                    appointmentId,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    revisitAppointmentId,
                    ServiceWarrantyClaimStatus.RevisitScheduled.ToString(),
                    "Teste",
                    "Agendado",
                    null,
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddDays(30),
                    DateTime.UtcNow.AddHours(48),
                    DateTime.UtcNow,
                    null,
                    null,
                    DateTime.UtcNow,
                    DateTime.UtcNow),
                new ServiceAppointmentDto(
                    revisitAppointmentId,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    ServiceAppointmentStatus.Confirmed.ToString(),
                    DateTime.UtcNow.AddDays(1),
                    DateTime.UtcNow.AddDays(1).AddHours(1),
                    null,
                    "Revisita de garantia",
                    null,
                    null,
                    null,
                    null,
                    null,
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    Array.Empty<ServiceAppointmentHistoryDto>())));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());
        var result = await controller.ScheduleWarrantyRevisit(
            appointmentId,
            claimId,
            new ScheduleServiceWarrantyRevisitRequestDto(
                DateTime.UtcNow.AddDays(1),
                DateTime.UtcNow.AddDays(1).AddHours(1),
                "Revisita tecnica"));

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    /// <summary>
    /// Cenario: a revisita de garantia eh solicitada para horario ja indisponivel.
    /// Passos: o teste injeta erro de slot unavailable no servico de reagendamento.
    /// Resultado esperado: o controller responde 409 Conflict.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Agendar warranty revisit | Deve retornar conflito quando slot unavailable")]
    public async Task ScheduleWarrantyRevisit_ShouldReturnConflict_WhenSlotIsUnavailable()
    {
        var appointmentId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.ScheduleWarrantyRevisitAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                claimId,
                It.IsAny<ScheduleServiceWarrantyRevisitRequestDto>()))
            .ReturnsAsync(new ServiceWarrantyRevisitOperationResultDto(
                false,
                ErrorCode: "warranty_revisit_slot_unavailable",
                ErrorMessage: "Sem disponibilidade."));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());
        var result = await controller.ScheduleWarrantyRevisit(
            appointmentId,
            claimId,
            new ScheduleServiceWarrantyRevisitRequestDto(
                DateTime.UtcNow.AddDays(1),
                DateTime.UtcNow.AddDays(1).AddHours(1),
                "Revisita tecnica"));

        Assert.IsType<ConflictObjectResult>(result);
    }

    /// <summary>
    /// Cenario: simulacao de politica financeira eh chamada sem identificacao do ator autenticado.
    /// Passos: o teste remove identidade da requisicao e aciona simulate financial policy.
    /// Resultado esperado: a resposta eh Unauthorized por ausencia de ator.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Simulate financial politica | Deve retornar nao autorizado quando actor missing")]
    public async Task SimulateFinancialPolicy_ShouldReturnUnauthorized_WhenActorIsMissing()
    {
        var controller = CreateController(Mock.Of<IServiceAppointmentService>());

        var result = await controller.SimulateFinancialPolicy(
            new ServiceFinancialCalculationRequestDto(
                ServiceFinancialPolicyEventType.ClientCancellation,
                150m,
                DateTime.UtcNow.AddHours(8),
                DateTime.UtcNow),
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    /// <summary>
    /// Cenario: o ator autenticado nao possui permissao para simular o tipo de evento financeiro solicitado.
    /// Passos: o teste combina role restrita com event type nao autorizado.
    /// Resultado esperado: o endpoint retorna 403 Forbid.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Simulate financial politica | Deve retornar forbid quando role nao pode simulate event type")]
    public async Task SimulateFinancialPolicy_ShouldReturnForbid_WhenRoleCannotSimulateEventType()
    {
        var controller = CreateController(
            Mock.Of<IServiceAppointmentService>(),
            Guid.NewGuid(),
            UserRole.Client.ToString());

        var result = await controller.SimulateFinancialPolicy(
            new ServiceFinancialCalculationRequestDto(
                ServiceFinancialPolicyEventType.ProviderNoShow,
                200m,
                DateTime.UtcNow.AddHours(4),
                DateTime.UtcNow),
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>
    /// Cenario: a simulacao financeira atende todas as regras e calcula valores com sucesso.
    /// Passos: o teste executa a acao com servico retornando simulacao valida.
    /// Resultado esperado: o controller responde 200 OK com resultado de calculo.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Simulate financial politica | Deve retornar ok quando calculation sucesso")]
    public async Task SimulateFinancialPolicy_ShouldReturnOk_WhenCalculationSucceeds()
    {
        var financialServiceMock = new Mock<IServiceFinancialPolicyCalculationService>();
        financialServiceMock
            .Setup(s => s.CalculateAsync(It.IsAny<ServiceFinancialCalculationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceFinancialCalculationResultDto(
                true,
                new ServiceFinancialCalculationBreakdownDto(
                    Guid.NewGuid(),
                    "Regra teste",
                    ServiceFinancialPolicyEventType.ClientCancellation,
                    200m,
                    10d,
                    4,
                    24,
                    1,
                    20m,
                    40m,
                    15m,
                    30m,
                    5m,
                    10m,
                    160m,
                    "Provider",
                    "memo")));

        var controller = CreateController(
            Mock.Of<IServiceAppointmentService>(),
            Guid.NewGuid(),
            UserRole.Client.ToString(),
            financialPolicyService: financialServiceMock.Object);

        var result = await controller.SimulateFinancialPolicy(
            new ServiceFinancialCalculationRequestDto(
                ServiceFinancialPolicyEventType.ClientCancellation,
                200m,
                DateTime.UtcNow.AddHours(10),
                DateTime.UtcNow),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ServiceFinancialCalculationBreakdownDto>(ok.Value);
        Assert.Equal("Regra teste", payload.RuleName);
    }

    /// <summary>
    /// Cenario: nao existe regra financeira cadastrada para o cenario simulado.
    /// Passos: o teste simula retorno de rule missing na camada de aplicacao.
    /// Resultado esperado: a acao retorna 404 NotFound.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Simulate financial politica | Deve retornar nao encontrado quando rule missing")]
    public async Task SimulateFinancialPolicy_ShouldReturnNotFound_WhenRuleIsMissing()
    {
        var financialServiceMock = new Mock<IServiceFinancialPolicyCalculationService>();
        financialServiceMock
            .Setup(s => s.CalculateAsync(It.IsAny<ServiceFinancialCalculationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceFinancialCalculationResultDto(
                false,
                ErrorCode: "policy_rule_not_found",
                ErrorMessage: "Regra nao encontrada."));

        var controller = CreateController(
            Mock.Of<IServiceAppointmentService>(),
            Guid.NewGuid(),
            UserRole.Provider.ToString(),
            financialPolicyService: financialServiceMock.Object);

        var result = await controller.SimulateFinancialPolicy(
            new ServiceFinancialCalculationRequestDto(
                ServiceFinancialPolicyEventType.ProviderCancellation,
                100m,
                DateTime.UtcNow.AddHours(6),
                DateTime.UtcNow),
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    /// <summary>
    /// Cenario: usuario nao administrador tenta executar override de politica financeira.
    /// Passos: o teste chama override com ator sem role de admin.
    /// Resultado esperado: o controller bloqueia com 403 Forbid.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Override financial politica | Deve retornar forbid quando actor nao admin")]
    public async Task OverrideFinancialPolicy_ShouldReturnForbid_WhenActorIsNotAdmin()
    {
        var appointmentId = Guid.NewGuid();
        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.OverrideFinancialPolicyAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                It.IsAny<ServiceFinancialPolicyOverrideRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Sem permissao."));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Provider.ToString());
        var result = await controller.OverrideFinancialPolicy(
            appointmentId,
            new ServiceFinancialPolicyOverrideRequestDto(
                ServiceFinancialPolicyEventType.ClientCancellation,
                "Ajuste excepcional aprovado pelo suporte.",
                DateTime.UtcNow));

        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>
    /// Cenario: admin realiza override financeiro para reprocessar efeito de politica no agendamento.
    /// Passos: o teste envia requisicao de override com ator administrativo e servico bem sucedido.
    /// Resultado esperado: o endpoint retorna 200 OK com resultado do reprocessamento.
    /// </summary>
    [Fact(DisplayName = "Servico appointments controller | Override financial politica | Deve retornar ok quando admin reprocesses")]
    public async Task OverrideFinancialPolicy_ShouldReturnOk_WhenAdminReprocesses()
    {
        var appointmentId = Guid.NewGuid();
        var appointment = new ServiceAppointmentDto(
            appointmentId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ServiceAppointmentStatus.Confirmed.ToString(),
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(3),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            Array.Empty<ServiceAppointmentHistoryDto>());

        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.OverrideFinancialPolicyAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                appointmentId,
                It.IsAny<ServiceFinancialPolicyOverrideRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(true, appointment));

        var controller = CreateController(serviceMock.Object, Guid.NewGuid(), UserRole.Admin.ToString());
        var result = await controller.OverrideFinancialPolicy(
            appointmentId,
            new ServiceFinancialPolicyOverrideRequestDto(
                ServiceFinancialPolicyEventType.ProviderNoShow,
                "Reprocessamento validado pela equipe de operacoes.",
                DateTime.UtcNow));

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ServiceAppointmentDto>(ok.Value);
        Assert.Equal(appointmentId, payload.Id);
    }

    private static ServiceAppointmentsController CreateController(
        IServiceAppointmentService service,
        Guid? userId = null,
        string? role = null,
        IServiceAppointmentChecklistService? checklistService = null,
        IServiceFinancialPolicyCalculationService? financialPolicyService = null,
        IFileStorageService? fileStorageService = null)
    {
        checklistService ??= Mock.Of<IServiceAppointmentChecklistService>();
        financialPolicyService ??= Mock.Of<IServiceFinancialPolicyCalculationService>();
        fileStorageService ??= Mock.Of<IFileStorageService>();

        var controller = new ServiceAppointmentsController(service, checklistService, financialPolicyService, fileStorageService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        if (userId.HasValue)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.Value.ToString())
            };

            if (!string.IsNullOrWhiteSpace(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(claims, "TestAuth"));
        }

        return controller;
    }
}
