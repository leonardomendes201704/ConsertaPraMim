# ST-099 - Exclusao operacional de lead no CPM Full

Status: Done
Epic: EPIC-TELEGRAM-002

## Objetivo

Permitir que a operacao exclua um lead diretamente no CPM Full para resetar testes e fluxos do Telegram sem depender de SQL manual.

## Entrega realizada

- O modal de detalhes do lead no Kanban passou a exibir o botao `Excluir lead`.
- A acao remove em transacao o lead local, o historico, o vinculo Telegram e as filas locais relacionadas.
- Quando o lead possui `TelegramChatId`, o CPM Full tenta resetar o handoff humano ativo no `TelegramBridge` antes de concluir a exclusao.
- A confirmacao da tela deixa explicito que contato e conversa no Chatwoot nao sao apagados automaticamente.

## Criterios de aceite atendidos

- A operacao consegue excluir o lead pelo proprio CPM Full.
- O reset do mesmo chat Telegram nao depende mais de SQL manual para limpar vinculo e filas locais.
- O handoff em memoria do `TelegramBridge` pode ser limpo no mesmo fluxo.
- O comportamento do Chatwoot fica explicito para evitar exclusao remota acidental.

## Tasks

- [x] Criar endpoint interno no `TelegramBridge` para resetar handoff por `TelegramChatId`.
- [x] Adicionar acao de exclusao no `KanbanController` e no modal do lead.
- [x] Implementar exclusao transacional no `SqlAdminKanbanService`.
- [x] Cobrir testes de regressao e atualizar manual/changelog/indice.
