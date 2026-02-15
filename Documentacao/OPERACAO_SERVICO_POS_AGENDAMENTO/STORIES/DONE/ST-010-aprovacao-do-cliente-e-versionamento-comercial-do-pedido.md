# ST-010 - Aprovacao do cliente e versionamento comercial do pedido

Status: Done  
Epic: EPIC-003

## Objetivo

Garantir que alteracoes comerciais durante o atendimento sejam aceitas explicitamente pelo cliente e refletidas com integridade no valor final do pedido.

## Criterios de aceite

- Cliente pode aprovar ou rejeitar aditivo com um clique e motivo opcional.
- Aprovacao atualiza valor total do pedido e registra versao comercial.
- Rejeicao devolve atendimento ao fluxo anterior sem alterar valor.
- Nao e permitido aplicar dois aditivos ativos simultaneos.
- Cliente visualiza comparativo `valor anterior` x `novo valor`.
- Admin pode auditar todas as versoes comerciais e decisoes.

## Tasks

- [x] Implementar endpoint de resposta do cliente ao aditivo.
- [x] Implementar estado de versao comercial no pedido.
- [x] Criar servico de recalculo do valor total consolidado.
- [x] Exibir comparativo visual de valores na UI cliente.
- [x] Exibir historico de versoes na UI prestador/admin.
- [x] Bloquear concorrencia com lock logico por pedido.
- [x] Ajustar notificacoes para aprovacao/rejeicao.
- [x] Criar testes de consistencia de versao e idempotencia.
- [x] Cobrir cenarios de timeout do aditivo pendente.
- [x] Atualizar runbook de suporte para divergencia de valor.
