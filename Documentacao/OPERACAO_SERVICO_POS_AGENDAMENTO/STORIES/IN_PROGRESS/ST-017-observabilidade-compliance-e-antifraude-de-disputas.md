# ST-017 - Observabilidade, compliance e antifraude de disputas

Status: In Progress  
Epic: EPIC-006

## Objetivo

Garantir monitoramento, rastreabilidade legal e deteccao de padroes suspeitos no fluxo de disputa, reduzindo abuso e risco regulatorio.

## Criterios de aceite

- Dashboard exibe volume, taxa de procedencia e tempo medio de resolucao.
- Alertas identificam usuarios com padrao anomalo de disputa.
- Logs criticos sao imutaveis e pesquisaveis por auditoria.
- Existe trilha de quem acessou e alterou cada caso.
- Politica de retencao de dados e aplicada ao modulo.
- Runbook de incidente/fraude esta documentado e validado.

## Tasks

- [x] Definir KPIs e consultas de observabilidade de disputa.
- [x] Implementar trilha de auditoria de acesso (view/edit/decision).
- [x] Criar regras basicas antifraude por frequencia e reincidencia.
- [x] Integrar alertas de anomalia no painel admin.
- [x] Implementar retencao e anonimizacao conforme politica LGPD.
- [x] Criar endpoint de auditoria com filtros por periodo/ator.
- [x] Criar testes de carga para consultas do painel.
- [x] Criar testes de seguranca de trilha imutavel.
- [x] Publicar runbook de fraude e compliance operacional.
- [ ] Atualizar manual QA com cenarios de abuso e escalonamento.
