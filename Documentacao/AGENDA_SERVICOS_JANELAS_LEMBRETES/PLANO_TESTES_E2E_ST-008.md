# Plano de Testes E2E - ST-008 (Agenda)

## Objetivo

Validar ponta a ponta o fluxo de agenda para cliente e prestador, cobrindo:

- consulta de horarios disponiveis;
- solicitacao e ciclo de vida do agendamento;
- confirmacao/recusa/reagendamento/cancelamento;
- operacao em campo (chegada, inicio, status operacional);
- lembretes e confirmacao de presenca;
- telemetria basica de observabilidade (correlation id e logs estruturados).

## Escopo funcional

- API: `ServiceAppointmentsController` e servicos associados.
- Portal Cliente: detalhes do pedido com secao de agendamento.
- Portal Prestador: dashboard, inbox de agenda e tela "Minha Agenda".
- Dashboard Admin: indicadores operacionais de agenda.

## Fora de escopo (nesta rodada)

- testes de carga/performance extensivos;
- caos engineering em infraestrutura;
- testes mobile nativos.

## Ambientes e pre-condicoes

1. Banco atualizado com migrations vigentes.
2. Seed habilitado com usuarios cliente e prestador ativos.
3. Prestador com regras de disponibilidade configuradas.
4. Cliente com pedido elegivel e proposta aceita para o prestador alvo.
5. Integracoes de notificacao ativas (hub/API) para validar eventos.

## Massa de dados recomendada

- 1 cliente (`Client`).
- 2 prestadores (`Provider`) na mesma cidade.
- 3 pedidos:
  - pedido A: sem agendamento (novo);
  - pedido B: com agendamento confirmado;
  - pedido C: com historico de reagendamento.
- janelas de disponibilidade:
  - seg-sex `08:00-12:00` e `13:00-18:00`;
  - bloqueio pontual em 1 faixa para validar conflitos.

## Matriz de cenarios E2E

### Bloco cliente

1. `E2E-CLI-001` - Buscar slots validos
- Passos:
  1. Cliente abre detalhes do pedido com proposta aceita.
  2. Seleciona prestador e data futura.
  3. Aciona "Buscar horarios disponiveis".
- Resultado esperado:
  - lista de slots retorna apenas intervalos livres;
  - nao exibe horarios bloqueados/ocupados.

2. `E2E-CLI-002` - Solicitar agendamento
- Passos:
  1. Cliente escolhe slot valido.
  2. Envia solicitacao de agendamento.
- Resultado esperado:
  - status inicial `PendingProviderConfirmation`;
  - feedback de sucesso no portal cliente;
  - prestador recebe indicativo de solicitacao pendente.

3. `E2E-CLI-003` - Solicitar reagendamento
- Passos:
  1. Com agendamento confirmado, cliente solicita nova janela valida.
  2. Informa motivo.
- Resultado esperado:
  - status de reagendamento pendente;
  - historico registra motivo e janela proposta.

4. `E2E-CLI-004` - Cancelar com politica valida
- Passos:
  1. Cliente cancela agendamento dentro da janela permitida.
- Resultado esperado:
  - cancelamento aceito;
  - status refletido para prestador e admin.

5. `E2E-CLI-005` - Confirmacao de presenca por lembrete
- Passos:
  1. Aguarda envio de lembrete de presenca.
  2. Cliente responde confirmando.
- Resultado esperado:
  - resposta de presenca registrada no agendamento;
  - indicador correspondente atualizado.

### Bloco prestador

1. `E2E-PRO-001` - Confirmar solicitacao pendente
- Passos:
  1. Prestador acessa inbox/minha agenda.
  2. Confirma solicitacao pendente.
- Resultado esperado:
  - status muda para `Confirmed`;
  - cliente visualiza confirmacao sem inconsistencias.

2. `E2E-PRO-002` - Recusar solicitacao
- Passos:
  1. Prestador recusa com motivo.
- Resultado esperado:
  - status muda para `RejectedByProvider`;
  - cliente recebe retorno da recusa.

3. `E2E-PRO-003` - Responder reagendamento
- Passos:
  1. Prestador recebe proposta de reagendamento.
  2. Aceita e, em ciclo separado, rejeita outro caso.
- Resultado esperado:
  - aceite aplica nova janela;
  - rejeicao preserva janela anterior;
  - ambos com trilha de historico.

4. `E2E-PRO-004` - Operacao em campo
- Passos:
  1. Prestador marca chegada.
  2. Inicia execucao.
  3. Atualiza status operacional ate `Completed`.
- Resultado esperado:
  - transicoes respeitam maquina de estados;
  - transicao invalida retorna conflito apropriado.

5. `E2E-PRO-005` - Conflito de slot
- Passos:
  1. Tentar agendar novo pedido no mesmo dia/horario de agendamento ja bloqueante.
- Resultado esperado:
  - API retorna conflito (`slot_unavailable`);
  - UI impede confirmacao de janela conflitante.

### Bloco admin/observabilidade

1. `E2E-ADM-001` - KPI operacional de agenda
- Passos:
  1. Executar cenarios de confirmacao, reagendamento, cancelamento e lembrete.
  2. Abrir dashboard admin no mesmo periodo.
- Resultado esperado:
  - card "Operacao da Agenda" exibe taxas e contadores coerentes.

2. `E2E-ADM-002` - Correlation id em request/response
- Passos:
  1. Chamar endpoint de agenda com header `X-Correlation-ID`.
  2. Chamar endpoint sem header.
- Resultado esperado:
  - com header: API devolve mesmo valor;
  - sem header: API gera valor novo (32 hex).

3. `E2E-ADM-003` - Logs estruturados
- Passos:
  1. Executar operacoes criticas (create/confirm/cancel/update status).
  2. Inspecionar logs da API.
- Resultado esperado:
  - logs contem `Operation`, `ActorUserId`, `ActorRole`, `AppointmentId`/`ServiceRequestId`, `ErrorCode` (quando houver) e `CorrelationId`.

## Criterios de aprovacao da rodada

- 100% dos cenarios criticos (`E2E-CLI-001..005`, `E2E-PRO-001..005`) aprovados.
- Sem regressao em endpoints de agenda ja existentes.
- Indicadores do dashboard admin coerentes com massa executada.
- Correlation id validado em requests com e sem header.

## Evidencias minimas por execucao

- print/video dos fluxos de UI cliente e prestador;
- payload de request/response dos endpoints chave;
- trecho de logs com mesmo `CorrelationId` percorrendo inicio/fim da operacao;
- snapshot do card "Operacao da Agenda" no admin.

## Roteiro de automacao incremental

1. Automatizar cenarios API-first (slots/create/confirm/reschedule/cancel).
2. Cobrir correlacao de headers e semantica de erros.
3. Expandir para smoke UI (cliente/prestador) em pipeline noturno.
4. Consolidar relatorio unico por build com taxa de sucesso e regressao.
