# ST-005 - Fluxo operacional de atendimento no app (chegada, inicio, checklist, evidencias)

Status: Done
Epic: EPIC-001

## Objetivo

Dar ao prestador capacidade de executar o atendimento de ponta a ponta no app, incluindo status operacional, checklist e evidencias.

## Criterios de aceite

- Endpoints dedicados para operacao de atendimento no app.
- Atualizacao de status operacional da visita.
- Preenchimento de checklist e upload de evidencias.

## Tasks

- [x] Mapear regras do fluxo operacional atual do portal.
- [x] Expor endpoints mobile provider dedicados para operacao.
- [x] Implementar telas de operacao no app.
- [x] Garantir auditoria, validacoes e mensagens de erro amigaveis.
- [x] Atualizar documentacao e diagramas.

## Mapeamento do fluxo atual (portal do prestador)

Origem do mapeamento:
- `Backend/src/ConsertaPraMim.Web.Provider/Controllers/ServiceRequestsController.cs`
- `Backend/src/ConsertaPraMim.Web.Provider/Views/ServiceRequests/Details.cshtml`
- `Backend/src/ConsertaPraMim.Web.Provider/Views/ServiceRequests/Agenda.cshtml`

### 1. Pontos de entrada operacionais

- `Agenda`: lista pendencias de confirmacao, proximas visitas e atalhos para acao rapida.
- `Details`: tela principal de execucao do atendimento por pedido/agendamento.

### 2. Regras de transicao operacional (estado do agendamento)

- Confirmar/Recusar visita:
  - permitido quando `appointment.Status == PendingProviderConfirmation`.
  - acoes: `ConfirmAppointment`, `RejectAppointment`.
- Registrar chegada (check-in):
  - permitido quando `appointment.Status == Confirmed` ou `RescheduleConfirmed`.
  - acao: `MarkArrival`.
  - aceita coordenadas (lat/long/precisao) e motivo manual opcional.
- Iniciar atendimento:
  - permitido quando `appointment.Status == Arrived`.
  - acao: `StartAppointment`.
- Atualizar andamento operacional:
  - acao: `UpdateAppointmentOperationalStatus`.
  - opcoes exibidas no portal: `OnTheWay`, `OnSite`, `InService`, `WaitingParts`, `Completed`.
  - bloqueado apenas para estados encerrados/cancelados (`RejectedByProvider`, `CancelledByClient`, `CancelledByProvider`, `ExpiredWithoutProviderAction`).
- Presenca:
  - acao: `RespondAppointmentPresence` (confirmar/nao confirmar).
  - indisponivel para estados encerrados/cancelados.
- Reagendamento iniciado pelo prestador:
  - acao: `RequestAppointmentReschedule`.
  - exige novo periodo (`proposedStartLocal`, `proposedEndLocal`) e `reason`.

### 3. Checklist tecnico e evidencias

- Checklist carregado por agendamento quando ha template obrigatorio.
- Atualizacao por item via `UpdateChecklistItem`.
- Campos por item:
  - `isChecked`
  - `note` (quando permitido)
  - evidencia opcional (`evidenceFile`)
  - `clearEvidence` para remover evidencia existente.
- Validacoes de evidencia (portal):
  - tamanho maximo: `25MB`.
  - extensoes permitidas: `.jpg`, `.jpeg`, `.png`, `.webp`, `.mp4`, `.webm`, `.mov`.
  - content-types permitidos: `image/jpeg`, `image/png`, `image/webp`, `video/mp4`, `video/webm`, `video/quicktime`.
  - validacao de assinatura/binario antes do upload.
- Armazenamento atual: bucket/pasta `service-checklists`.

### 4. Visibilidade operacional exibida no portal

- Status do agendamento + status operacional.
- Risco no-show (nivel, score, motivos e timestamp de calculo).
- Historico do agendamento (transicoes de status e status operacional).
- Informacoes de check-in (hora, GPS e motivo manual quando houver).
- Termo de conclusao (quando existente) exibido para acompanhamento.

### 5. Metas de paridade para o app (base para as proximas tasks)

- Reproduzir no app as mesmas regras de habilitacao de acoes por status.
- Manter as mesmas validacoes de checklist/evidencias.
- Expor contratos mobile dedicados sem reusar endpoints dos portais.
- Preservar trilha de auditoria ja existente nos servicos de agendamento/checklist.

## Implementacao parcial concluida

- Endpoints mobile de operacao publicados:
  - `POST /api/mobile/provider/agenda/{appointmentId}/arrive`
  - `POST /api/mobile/provider/agenda/{appointmentId}/start`
  - `POST /api/mobile/provider/agenda/{appointmentId}/operational-status`
  - `GET /api/mobile/provider/agenda/{appointmentId}/checklist`
  - `POST /api/mobile/provider/agenda/{appointmentId}/checklist/items`
  - `POST /api/mobile/provider/agenda/checklist-evidences/upload`
- App do prestador atualizado na tela `Agenda` para:
  - registrar chegada, iniciar atendimento e atualizar status operacional;
  - carregar checklist tecnico por agendamento;
  - preencher itens do checklist e enviar evidencia;
  - limpar evidencia previamente vinculada ao item.

## Garantias de auditoria, validacao e UX de erro

- Auditoria preservada por design:
  - operacoes de agenda mobile utilizam `ServiceAppointmentService`, mantendo historico de transicoes e trilha operacional;
  - checklist mobile utiliza `ServiceAppointmentChecklistService`, mantendo historico de respostas e evidencias.
- Validacoes tecnicas implementadas:
  - arquivo de evidencia validado no app e no backend (extensao, content-type e limite de 25MB);
  - bloqueio de salvamento de item obrigatorio com evidencia quando marcado sem arquivo;
  - validacao de motivo para status operacional `WaitingParts`.
- Mensagens de erro amigaveis:
  - mapeamento de `errorCode` de negocio para mensagens claras no app;
  - exibicao de mensagens locais no card operacional/checklist e mensagens globais do app para falhas de API.
