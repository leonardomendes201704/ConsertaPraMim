# ST-093 - Lead Telegram sem telefone/e-mail sincroniza no Chatwoot via identificador tecnico

## Contexto

Depois que o bot publicado passou a criar o lead tecnico no CPM Full a partir da primeira mensagem publica do Telegram, o funil passou a registrar corretamente o lead, mas a sincronizacao com o Chatwoot ainda falhava quando o usuario nao havia informado telefone ou e-mail no primeiro contato.

## Problema observado

- O lead `Source = Telegram` aparecia no funil com `Vinculo Telegram` preenchido.
- A sync do Chatwoot retornava `Lead sem telefone ou e-mail valido para sincronizar com Chatwoot.`.
- Como resultado, a conversa humana nao era criada ou reaproveitada no Chatwoot mesmo com `TelegramChatId` disponivel.

## Causa raiz

O `ChatwootLeadSyncService` ainda exigia telefone ou e-mail para qualquer lead antes de montar o `ChatwootUpsertContactRequest`, ignorando o fato de que a trilha Telegram ja persistia `TelegramChatId`, `ChatbotConversationId` e `ChannelConversationId` no vinculo tecnico do lead.

## Objetivo

Permitir que leads originados do Telegram sincronizem no Chatwoot mesmo no primeiro contato, usando o identificador tecnico do bot como fallback deterministico para o contato, sem relaxar a regra de validacao para outras origens.

## Entrega aplicada

1. O `ChatwootLeadSyncService` passou a aceitar fallback de identificador para leads `Source = Telegram`, na ordem `TelegramChatId`, `ChatbotConversationId` e `ChannelConversationId`.
2. O contato do Chatwoot agora recebe `additional_attributes` tecnicos do Telegram para rastreabilidade interna.
3. A anotacao privada de abertura da conversa passou a informar quando o primeiro contato ainda nao trouxe telefone ou e-mail.
4. O `ChatwootBackfillService` alinhou o `dry-run` para marcar esses leads Telegram como elegiveis quando houver vinculo tecnico suficiente.
5. Entraram testes de regressao para a sync do lead Telegram sem telefone/e-mail e para o dry-run do backfill com identificador tecnico do bot.

## Validacao esperada

1. Enviar nova mensagem para `@chatwootcpm_bot` em conversa ainda sem telefone/e-mail cadastrado.
2. Confirmar no CPM Full que o lead continua com `Source = Telegram`.
3. Confirmar no modal do lead que `Sync Chatwoot = Sincronizado`.
4. Confirmar no Chatwoot a criacao ou reaproveitamento do contato/conversa usando identificador tecnico do Telegram.
5. Validar que a nota privada de abertura registra a ausencia temporaria de telefone/e-mail.

## Risco

- Alto no ambiente publicado, porque o defeito bloqueava o bootstrap do atendimento humano para o principal caso de uso do primeiro contato Telegram.
