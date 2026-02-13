# EPIC-001 - Presenca operacional do prestador e leitura de mensagens

## Objetivo

Evoluir a experiencia de operacao e comunicacao em tempo real entre cliente e prestador, com status operacional explicito e feedback de leitura das mensagens.

## Problema atual

- O prestador nao possui status operacional persistido (Ausente, Online, EmAtendimento).
- O badge "Online" atual e apenas visual, sem estado de negocio.
- O chat nao exibe estados de entrega/leitura da mensagem.
- Falta previsibilidade para cliente e prestador sobre disponibilidade e acompanhamento de conversa.

## Resultado esperado

- Prestador pode alterar status operacional a qualquer momento.
- Status fica persistido e refletido em tempo real nos dois portais.
- Mensagens do chat exibem estado de envio, entrega e leitura.
- Historico do chat preserva status de leitura apos reconexao/reload.

## Metricas de sucesso

- 100% das mudancas de status operacional refletidas nos portais em ate 2 segundos.
- 100% das mensagens com estado coerente entre backend e frontend.
- 0 regressao no envio de mensagens e anexos.
- Cobertura de testes para regras novas (unitarios + integracao).

## Escopo

### Inclui

- Modelo de dominio para status operacional do prestador.
- Endpoints e SignalR para alterar e propagar status operacional.
- Modelo de dominio para recibo de leitura no chat.
- Eventos/handlers SignalR para marcar mensagem como entregue/lida.
- Atualizacoes de UI nos portais cliente e prestador.
- Testes automatizados e documentacao tecnica.

### Nao inclui

- Indicador de "digitando...".
- Push notification mobile nativo.
- Integracao com canais externos de mensageria (WhatsApp oficial, SMS).

## Historias vinculadas

- ST-001 - Status operacional do prestador.
- ST-002 - Confirmacao de leitura no chat.
