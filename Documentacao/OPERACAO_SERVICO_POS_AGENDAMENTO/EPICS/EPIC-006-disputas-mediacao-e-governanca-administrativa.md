# EPIC-006 - Disputas, mediacao e governanca administrativa

## Objetivo

Disponibilizar um fluxo robusto de disputa e mediacao para casos de desacordo, com decisao rastreavel e impacto financeiro controlado.

## Problema atual

- Casos de conflito dependem de tratativa ad hoc.
- Falta esteira formal de abertura, analise, decisao e fechamento.
- Risco juridico e reputacional cresce sem trilha de auditoria.

## Resultado esperado

- Cliente/prestador podem abrir disputa com evidencias.
- Admin opera uma fila de mediacao com SLA e checklist decisorio.
- Decisoes aplicam ajustes financeiros de forma automatica e auditavel.
- Dashboards identificam padroes de abuso/fraude para prevencao.

## Escopo

### Inclui

- Abertura de disputa com tipos e evidencias.
- Backoffice de mediacao admin.
- Outcomes financeiros (reembolso parcial/total, credito, debito).
- Logs de auditoria e relatorios operacionais.

### Nao inclui

- Integracao com arbitragem externa/judicial.
- Automacao total da decisao sem avaliacao humana.

## Metricas de sucesso

- Tempo medio de resolucao de disputa < 72h.
- >= 95% das disputas com decisao e justificativa registradas.
- Queda de reincidencia de disputas por mesmo motivo em >= 20%.

## Historias vinculadas

- ST-015 - Abertura de disputa com evidencias.
- ST-016 - Esteira de mediacao admin e decisoes financeiras.
- ST-017 - Observabilidade, compliance e antifraude de disputas.
