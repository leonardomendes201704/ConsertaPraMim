# Manual Admin/QA - Agenda (ST-008)

## Objetivo

Consolidar como o time administrativo e QA deve operar, validar e auditar o modulo de agenda apos as evolucoes de observabilidade.

## Perfis e responsabilidades

- Admin:
  - acompanha KPIs operacionais no dashboard;
  - investiga desvios (confirmacao, reagendamento, cancelamento, lembretes);
  - aciona times de suporte quando necessario.
- QA:
  - executa cenarios E2E regulares;
  - valida correlacao de logs com `X-Correlation-ID`;
  - certifica nao regressao funcional em cliente e prestador.
- Suporte Operacional:
  - usa runbook para incidente;
  - organiza evidencias e timeline para RCA.

## Onde validar no portal admin

1. Dashboard Admin (`Home`):
   - card `Operacao da Agenda` com:
     - taxa de confirmacao no SLA;
     - taxa de reagendamento;
     - taxa de cancelamento;
     - taxa de falha de lembretes;
     - tentativas e falhas de lembretes no periodo.
2. Filtros de periodo:
   - sempre alinhar janela de analise com o periodo dos eventos testados.

## Checklist funcional de auditoria rapida

1. Criar/agendar caso de teste no cliente.
2. Confirmar e operar no prestador.
3. Recarregar dashboard admin no periodo alvo.
4. Validar mudanca coerente dos KPIs.
5. Coletar `X-Correlation-ID` de pelo menos 1 fluxo completo.
6. Confirmar logs de inicio/sucesso/erro no backend com mesmo id.

## Procedimento de QA (resumo operacional)

1. Rodar bloco cliente (`E2E-CLI-*`) do plano ST-008.
2. Rodar bloco prestador (`E2E-PRO-*`) do plano ST-008.
3. Rodar bloco admin/observabilidade (`E2E-ADM-*`) do plano ST-008.
4. Comparar resultados com criterios de aprovacao.

Referencia oficial: `PLANO_TESTES_E2E_ST-008.md`.

## Evidencias obrigatorias para aceite

- capturas dos principais passos em cliente/prestador/admin;
- request/response de endpoints chave com `X-Correlation-ID`;
- trecho de logs estruturados contendo:
  - `Operation`
  - `ActorUserId`
  - `ActorRole`
  - `AppointmentId` ou `ServiceRequestId`
  - `ErrorCode` (quando houver)
  - `CorrelationId`
- snapshot final dos KPIs no admin.

## Cenarios de regressao minima por release

1. Slots disponiveis coerentes com disponibilidade e bloqueios.
2. Criacao de agendamento sem conflito.
3. Confirmacao do prestador refletida no cliente.
4. Reagendamento (solicitacao + resposta) com historico.
5. Cancelamento com politica valida.
6. Presenca e status operacional atualizando corretamente.
7. KPI admin atualizado para o periodo exercitado.
8. Correlation id propagado e logado.

## Criterio de aprovacao operacional

- nenhum cenario critico com falha bloqueante;
- sem inconsistencia entre estado do agendamento e KPI agregado;
- runbook pronto para uso em incidente;
- evidencias anexadas ao dossie da release.
