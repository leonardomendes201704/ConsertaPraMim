# ST-031 - Central de atendimento no app admin mobile

Status: Done
Epic: EPIC-010

## Objetivo

Permitir operacao basica da fila de suporte no mobile admin: listar chamados, abrir detalhe, responder, atribuir e atualizar status.

## Criterios de aceite

- Lista de chamados via `/api/admin/support/tickets`.
- Detalhe de chamado via `/api/admin/support/tickets/{ticketId}`.
- Admin consegue enviar mensagem, atribuir para si e atualizar status.
- Atualizacao da UI ocorre apos cada acao sem reiniciar app.

## Tasks

- [x] Implementar consumo da fila e detalhe de chamados.
- [x] Criar telas `SupportTickets` e `SupportTicketDetails`.
- [x] Implementar acoes de resposta, atribuicao e status.
- [x] Integrar navegacao e refresh incremental da fila.
