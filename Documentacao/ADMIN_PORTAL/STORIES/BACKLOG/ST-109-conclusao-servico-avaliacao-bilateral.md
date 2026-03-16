# ST-109 - Conclusao do servico e avaliacao bilateral

Status: Backlog
Epic: EPIC-JORNADA-001

## Objetivo

Fechar a jornada apos o atendimento com confirmacao de conclusao e avaliacao do cliente sobre o prestador e do prestador sobre o cliente.

## Criterios de aceite

- O sistema consegue marcar servico como concluido.
- Cliente e prestador recebem solicitacao de avaliacao.
- As duas avaliacoes ficam vinculadas ao mesmo caso.
- O Kanban fecha a jornada automaticamente.

## Tasks

- [ ] Definir evento de conclusao e suas origens validas.
- [ ] Criar fluxo de cobranca de avaliacao para cliente.
- [ ] Criar fluxo de cobranca de avaliacao para prestador.
- [ ] Persistir avaliacoes, comentarios e motivos de nota baixa.
- [ ] Atualizar score operacional do prestador e historico do cliente.
- [ ] Cobrir no-show, cancelamento tardio e servico contestado.
