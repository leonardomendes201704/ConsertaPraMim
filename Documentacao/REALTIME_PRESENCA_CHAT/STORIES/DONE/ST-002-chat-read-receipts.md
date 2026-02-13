# ST-002 - Confirmacao de leitura no chat (enviado, entregue, lido)

Status: Done  
Epic: EPIC-001

## Objetivo

Implementar recibos de mensagem no chat em tempo real, com estados de envio, entrega e leitura, de forma persistente e consistente nos portais cliente e prestador.

## Criterios de aceite

- Cada mensagem possui estado de rastreio: `enviado`, `entregue` e `lido`.
- O estado de mensagem e atualizado em tempo real via SignalR.
- O estado `lido` e marcado quando o destinatario abre/visualiza a conversa.
- Recarregar a pagina nao perde historico de estado de leitura.
- UI mostra icones/indicadores claros e consistentes nos dois portais.
- A logica respeita autorizacao da conversa (somente participantes).
- Envio de anexos continua funcionando sem regressao.
- Testes automatizados cobrem fluxo nominal e casos de borda.

## Tasks

- [x] Definir modelo de dominio para estado de entrega/leitura (timestamps e/ou flags por mensagem).
- [x] Criar migration para persistir os campos de recibo no banco.
- [x] Evoluir `ChatService` para atualizar e consultar recibos.
- [x] Adicionar metodos no `ChatHub` para marcar mensagens como entregues/lidas.
- [x] Publicar evento SignalR de atualizacao de recibo para ambos participantes.
- [x] Atualizar layout de chat no portal cliente com check simples/dobro/azul.
- [x] Atualizar layout de chat no portal prestador com a mesma regra visual.
- [x] Tratar reconexao para re-sincronizar estado de leitura pendente.
- [x] Adicionar testes unitarios para regras de transicao de estado.
- [x] Adicionar testes de integracao para persistencia e sincronizacao de recibos.
