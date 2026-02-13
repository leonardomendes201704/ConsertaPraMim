# ST-010 - Auditoria, QA, rollout e desativacao do admin embutido

Status: Backlog  
Epic: EPIC-001

## Objetivo

Finalizar o projeto com qualidade, rastreabilidade e transicao segura para o novo portal admin.

## Criterios de aceite

- Toda acao administrativa sensivel gera trilha de auditoria persistida.
- Suite minima de testes de integracao e autorizacao executando.
- Rollout controlado com plano de rollback.
- Admin antigo no portal do prestador removido ou desativado por feature flag.

## Tasks

- [ ] Criar entidade/repositorio de auditoria administrativa.
- [ ] Registrar `quem`, `quando`, `o que`, `antes` e `depois` em cada acao.
- [ ] Criar testes automatizados para rotas e regras de seguranca.
- [ ] Revisar logs estruturados para operacao e incidentes.
- [ ] Definir feature flag para desativar admin embutido no prestador.
- [ ] Publicar checklist de deploy e rollback.
- [ ] Registrar versoes e entregas no changelog.

