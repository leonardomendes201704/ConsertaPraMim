# ST-026 - Auditoria, QA e rollout do modulo de suporte

Status: Backlog  
Epic: EPIC-008

## Objetivo

Como time de plataforma, quero concluir com qualidade e governanca o modulo de suporte para publicar com risco controlado.

## Criterios de aceite

- Trilha de auditoria cobre as operacoes administrativas sensiveis do modulo.
- Existe cobertura de testes para fluxos principais e permissoes.
- Runbook de deploy/rollback do modulo documentado.
- Changelog e documentacao operacional atualizados.
- Go-live realizado sem regressao critica nos portais.

## Tasks

- [ ] Revisar e completar auditoria de acoes sensiveis (atribuir/status/fechar/reabrir).
- [ ] Criar suite de testes E2E do fluxo ponta a ponta prestador <-> admin.
- [ ] Executar bateria de regressao focada em autorizacao e isolamento de dados.
- [ ] Criar runbook de deploy/rollback especifico do modulo de suporte.
- [ ] Atualizar `CHANGELOG/CHANGELOG.md` e documentacao funcional.
- [ ] Definir checklist de monitoramento pos-deploy (erros, tempo de resposta, fila).
- [ ] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [ ] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.
