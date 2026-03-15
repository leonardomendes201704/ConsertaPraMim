# Roteiro de Teste E2E - Telegram em Producao

## Objetivo

Validar de forma curta e repetivel a trilha publicada `Telegram -> funil CPM -> Chatwoot -> handoff -> Telegram`.

## Pre-check

1. Confirmar `https://www.consertapramim.com/health` com retorno `Healthy`.
2. Confirmar `https://telegram.consertapramim.com/health` com retorno `Healthy`.
3. Testar sempre com mensagem nova no bot publicado `@chatwootcpm_bot`.

## Teste 1 - Cliente

1. No Telegram, enviar `Preciso de ajuda com meu chuveiro`.
2. Esperado no Telegram: ACK inicial do bot.
3. No CPM Full, abrir `/admin/funil/clientes`.
4. Esperado no funil:
- lead novo com `Source = Telegram`
- secao `Vinculo Telegram` preenchida
- `Chat ID Telegram` mascarado
- `Sync Chatwoot = Sincronizado`
5. No Chatwoot, esperado:
- conversa criada ou reaproveitada na inbox `CPM Clientes`
- contato/conversa com `Canal de Origem = Telegram`
- se nao houver telefone/e-mail ainda, contato criado por identificador tecnico

## Teste 2 - Telegram para Chatwoot

1. Na mesma conversa do Telegram, enviar uma segunda mensagem.
2. Esperado no Chatwoot:
- nova mensagem `incoming` na mesma conversa
- sem duplicidade
3. No CPM Full, esperado:
- `Ultima msg Telegram sincronizada` preenchida
- historico de sincronizacao registrado no lead

## Teste 3 - Handoff humano

1. No Chatwoot, responder publicamente na conversa originada do Telegram.
2. Esperado no Telegram:
- a resposta chega no mesmo chat
3. No CPM Full, esperado:
- `Handoff humano iniciado` preenchido
- `Ultima msg Chatwoot sincronizada` preenchida
- historico de handoff e sincronizacao registrado

## Teste 4 - Prestador

1. Em outro chat de teste, enviar `Quero me cadastrar como prestador parceiro`.
2. No CPM Full, abrir `/admin/funil/prestadores`.
3. Esperado:
- lead novo em `prestadores`
- `Source = Telegram`
- `Vinculo Telegram` preenchido
4. No Chatwoot, esperado:
- conversa criada ou reaproveitada na inbox `CPM Prestadores`

## Validacao final

1. Cliente cai em `clientes`.
2. Prestador cai em `prestadores`.
3. O mesmo chat reaproveita o mesmo lead e a mesma conversa humana.
4. Mensagens do Telegram entram no Chatwoot.
5. Respostas humanas do Chatwoot voltam ao Telegram.

## Troubleshooting rapido

- Se nao criar lead: abrir o drawer `Diagnostico Telegram` no Kanban.
- Se criar lead, mas nao abrir Chatwoot: usar `Sincronizar Chatwoot` no modal do lead.
- Se a resposta humana nao voltar: confirmar que a mensagem no Chatwoot foi publica, nao nota privada.
- Se o teste foi feito antes de um hotfix recente: reenviar mensagem nova; mensagens antigas consumidas nao sao reprocessadas sozinhas.
