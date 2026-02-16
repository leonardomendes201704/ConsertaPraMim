# RUNBOOK - Fraude e Compliance de Disputas (ST-017)

## 1. Objetivo

Padronizar a operacao de prevencao, investigacao e resposta a risco de fraude no modulo de disputas, garantindo:

- rastreabilidade completa de acesso e decisao administrativa;
- consistencia de resposta para padroes anomalos;
- conformidade com politica de retencao e anonimização LGPD.

## 2. Escopo

Aplicavel ao fluxo administrativo de disputas no portal admin:

- fila operacional;
- observabilidade e anomalias;
- trilha de auditoria;
- decisao administrativa;
- rotina de retencao/anonimizacao.

## 3. Perfis e responsabilidades

- Operador de mediacao (Admin):
  - triar alertas e casos;
  - registrar workflow/decisao com justificativa;
  - manter evidencias no caso.
- Lider de operacao:
  - validar escalonamentos de severidade alta/critica;
  - aprovar acoes corretivas sistêmicas.
- Compliance/Seguranca:
  - revisar trilhas e padroes de abuso;
  - auditar execucao de retencao LGPD.

## 4. Fontes oficiais de verdade

- Fila de disputas:
  - `GET /api/admin/disputes/queue`
- KPIs e anomalias:
  - `GET /api/admin/disputes/observability`
- Trilha consolidada de auditoria:
  - `GET /api/admin/disputes/audit`
- Detalhe do caso:
  - `GET /api/admin/disputes/{id}`
- Workflow operacional:
  - `PUT /api/admin/disputes/{id}/workflow`
- Decisao final:
  - `POST /api/admin/disputes/{id}/decision`
- Retencao LGPD:
  - `POST /api/admin/disputes/compliance/retention/run`

## 5. Sinais de risco monitorados

Alertas antifraude atuais:

- `HIGH_DISPUTE_FREQUENCY`
- `HIGH_REJECTED_RATE`
- `REPEAT_REASON_PATTERN`

Campos de apoio:

- severidade (`low`, `medium`, `high`, `critical`);
- usuario afetado e role;
- janela de observacao (dias);
- metrica observada x threshold;
- lista de disputas recentes.

## 6. Matriz de severidade e SLA de resposta

- Baixa:
  - confirmar contexto em ate 24h.
- Media:
  - triagem detalhada em ate 8h.
- Alta:
  - iniciar investigacao imediata e registrar plano em ate 2h.
- Critica:
  - acionar lider/compliance em ate 30 min e abrir incidente operacional.

## 7. Procedimento operacional (passo a passo)

### Etapa A - Triagem inicial

1. Abrir painel de disputas.
2. Consultar bloco de anomalias em observabilidade.
3. Identificar usuario/casos com maior severidade.
4. Registrar no ticket interno:
   - alertCode;
   - usuario;
   - timestamp de inicio da analise.

### Etapa B - Coleta de evidencias

1. Abrir o caso em detalhe.
2. Exportar/registrar:
   - mensagens e anexos;
   - historico de workflow;
   - decisoes anteriores.
3. Consultar trilha consolidada:
   - `GET /api/admin/disputes/audit?disputeCaseId={id}`
4. Filtrar por ator quando necessario:
   - `GET /api/admin/disputes/audit?actorUserId={id}&fromUtc=...&toUtc=...`

### Etapa C - Classificacao do evento

Classificar em uma das categorias:

- comportamento suspeito sem impacto confirmado;
- abuso operacional confirmado (uso indevido recorrente);
- falso positivo.

### Etapa D - Acao corretiva

1. Atualizar workflow do caso (`UnderReview` ou `WaitingParties`).
2. Registrar nota objetiva com evidencias.
3. Se houver decisao final:
   - executar `decision` com justificativa completa;
   - incluir impacto financeiro quando aplicavel.
4. Garantir que o evento ficou na trilha:
   - case audit (`dispute_workflow_updated` / `dispute_decision_recorded`);
   - admin audit (`DisputeWorkflowUpdated` / `DisputeDecisionRecorded`).

### Etapa E - Encerramento e aprendizado

1. Verificar se o caso foi encerrado com status correto.
2. Atualizar registro interno com causa raiz.
3. Propor ajuste de regra/threshold se houver padrao novo.

## 8. Checklist obrigatorio de auditoria

Antes de encerrar qualquer incidente de fraude/compliance, confirmar:

- trilha de acesso ao caso registrada (`dispute_case_viewed`);
- alteracoes de workflow com metadata e ator;
- decisao final com justificativa e outcome;
- horario UTC e ator presentes nos eventos;
- relatorio/ticket interno com vinculacao ao `disputeCaseId`.

## 9. Politica de retencao e LGPD

Fluxo recomendado:

1. Rodar simulacao:
   - `POST /api/admin/disputes/compliance/retention/run` com `dryRun=true`
2. Validar volume de candidatos.
3. Executar anonimizacao:
   - `dryRun=false`
4. Confirmar eventos de auditoria:
   - `dispute_lgpd_anonymized`
   - `DisputeLgpdRetentionRun`

Regra operacional:

- nunca executar em horario de pico sem dry-run previo;
- registrar data/hora, executor e resultado (candidates/anonymized).

## 10. Evidencias minimas para QA/Compliance

- print/export da observabilidade com alerta ativo;
- consulta de auditoria com filtros aplicados;
- detalhe de caso antes/depois da decisao;
- payload de retencao (dry-run e execucao real, quando houver).

## 11. Indicadores de acompanhamento continuo

- volume diario de alertas por tipo;
- taxa de confirmacao de abuso por alerta;
- tempo medio entre alerta e primeira acao;
- reincidencia por usuario em 30 dias;
- cobertura de trilha auditavel por caso encerrado.

## 12. Falhas operacionais proibidas

- decidir caso sem justificativa textual;
- alterar status fora do fluxo permitido;
- fechar investigacao sem consultar trilha de auditoria;
- executar retencao LGPD sem simulacao previa.
