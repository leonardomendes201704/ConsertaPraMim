# ST-018 - Bloqueio de novo pedido por avaliacao pendente do cliente

Status: In Progress  
Epic: EPIC-005

## Objetivo

Impedir que o cliente publique um novo pedido enquanto houver servicos concluidos sem avaliacao enviada, forçando o fechamento do ciclo de qualidade e reputacao antes da abertura de nova demanda.

## Criterios de aceite

- Ao abrir `ServiceRequests/Create`, o portal do cliente deve consultar pedidos concluidos/validados sem avaliacao enviada pelo cliente.
- Quando houver pendencia, a tela deve abrir um modal bloqueante exigindo avaliacao antes de prosseguir.
- Enquanto existir pelo menos 1 pedido pendente de avaliacao, o cliente nao pode publicar novo pedido.
- A submissao da avaliacao dentro do modal deve atualizar a fila de pendencias sem exigir navegacao manual para outra tela.
- Quando a ultima pendencia for resolvida, o modal deve fechar e o wizard de novo pedido volta a operar normalmente.
- O bloqueio deve ser aplicado tambem no `POST /ServiceRequests/Create`, evitando bypass por submit manual.
- O motivo do bloqueio precisa ser objetivo em PT-BR e orientado ao usuario.

## Tasks

- [ ] Registrar backlog, diagrama e indice da trilha para o bloqueio por avaliacao pendente.
- [ ] Aplicar bloqueio server-side em `ServiceRequests/Create` quando existir review pendente.
- [ ] Criar acao web para enviar avaliacao pendente sem sair do fluxo de criacao.
- [ ] Exibir modal bloqueante no wizard de criacao com fila de pendencias e submissao inline.
- [ ] Atualizar QA/operacao com cenarios de bloqueio, desbloqueio e tentativa de bypass.
- [ ] Adicionar teste de regressao para garantir que o cliente nao cria novo pedido com review pendente.

## Manual QA a atualizar

- `RUNBOOK_QA_AVALIACAO_BILATERAL_ST-013.md`
