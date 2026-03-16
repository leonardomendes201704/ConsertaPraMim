# ST-111 - Hotfix da continuacao de qualificacao do Telegram apos captura de contato

Status: Done
Epic: EPIC-JORNADA-001

## Objetivo

Corrigir o bot Telegram para que ele continue a conversa de qualificacao depois que o telefone ou o e-mail ja estiverem persistidos no lead, sem travar quando a mensagem seguinte trouxer apenas parte dos dados.

## Criterios de aceite

- O bridge considera telefone e e-mail ja persistidos no CPM Full ao montar a proxima resposta.
- O bridge considera tambem cidade e categoria ja qualificadas no lead ou na jornada.
- Uma mensagem como `Praia Grande` apos o envio do telefone volta a gerar resposta automatica.
- O bot nao repete indevidamente a solicitacao de telefone quando o contato ja existe.
- Testes de regressao cobrem o retorno do estado persistido entre CPM Full e TelegramBridge.

## Tasks

- [x] Propagar estado persistido de telefone e e-mail no contrato interno do Telegram.
- [x] Propagar tambem cidade e categoria ja qualificadas no retorno do upsert.
- [x] Ajustar o `TelegramInboundUpdateProcessor` para montar a resposta com base no estado efetivo do lead, nao apenas na mensagem atual.
- [x] Cobrir regressao em `TelegramLeadAutomationServiceTests` e `TelegramInboundUpdateProcessorTests`.
- [x] Atualizar changelog, indice e manual operacional no mesmo ciclo.

## Entrega implementada

- O `TelegramLeadAutomationService` passou a retornar `HasPhone`, `HasEmail`, `HasCity` e `HasServiceCategory` com base no lead/jornada ja persistidos no CPM Full.
- O endpoint interno `POST /api/integrations/telegram/automation/lead` agora devolve esse snapshot para o `TelegramBridge`.
- O `TelegramInboundUpdateProcessor` passou a montar a proxima pergunta usando o estado efetivo do lead, evitando silencio do bot em mensagens parciais apos a captura de contato.
- O fluxo deixa de depender exclusivamente dos dados presentes na ultima mensagem para decidir se deve continuar qualificando ou apenas oferecer envio opcional de e-mail.
