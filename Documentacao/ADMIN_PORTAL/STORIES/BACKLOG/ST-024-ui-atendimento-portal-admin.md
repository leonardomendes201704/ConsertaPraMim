# ST-024 - UI de atendimento no portal admin

Status: Backlog  
Epic: EPIC-008

## Objetivo

Como admin, quero um painel de atendimento para operar chamados de prestadores com foco em fila e produtividade.

## Criterios de aceite

- Existe item de menu dedicado para atendimento de prestadores.
- Tela principal mostra fila com filtros, ordenacao e pagina.
- Admin abre detalhe do chamado e visualiza historico completo.
- Admin responde chamado, altera status e atribui responsavel pela UI.
- Feedback visual de atualizacao de status e erros de operacao.

## Tasks

- [ ] Criar menu e rota de atendimento no `ConsertaPraMim.Web.Admin`.
- [ ] Implementar grid de fila com filtros (status, prioridade, atribuicao, busca).
- [ ] Implementar painel de detalhe de chamado com mensagens.
- [ ] Implementar acoes de responder, alterar status e atribuir.
- [ ] Integrar com endpoints admin de ST-022.
- [ ] Incluir indicadores de fila (abertos, sem resposta, em atraso).
- [ ] Garantir acessibilidade basica e responsividade.
- [ ] Criar testes de regressao da tela de atendimento admin.
- [ ] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [ ] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.
