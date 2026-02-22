# EPIC-012 - Webmail Admin E2E (SMTP/POP3 Gmail)

Status: In Progress
Owner: Admin Platform

## Objetivo

Disponibilizar um fluxo de webmail administrativo ponta a ponta para operacao, permitindo envio e acompanhamento de emails entre administracao, clientes e prestadores, com suporte inicial a Gmail (SMTP + POP3), notificacoes em tempo real no portal admin e push/toast no app admin mobile.

## Escopo

- Backend admin mailbox (`/api/admin/mailbox/*`) com:
  - configuracao SMTP/POP3,
  - envio via SMTP,
  - sincronizacao inbox via POP3,
  - listagem/detalhe de mensagens,
  - marcacao de leitura.
- Portal admin:
  - menu "Webmail",
  - inbox,
  - compose,
  - configuracao de credenciais e parametros de sync.
- App admin mobile:
  - aba "Webmail",
  - inbox com detalhe,
  - compose,
  - sincronizacao manual e polling leve.
- Notificacoes:
  - novo email inbound notifica administradores via realtime/push.
- Operacao:
  - runbook de setup Gmail e troubleshooting.

## Criterios de sucesso

- Admin consegue configurar mailbox e enviar email para cliente/prestador sem usar ferramentas externas.
- Inbox admin recebe emails sincronizados e exibidos no portal e no app mobile admin.
- Eventos de novo email inbound chegam como notificacao no portal e push/toast no app admin mobile.
- Fluxo opera sem indisponibilizar API em erro de autenticacao/rede de SMTP/POP3.

## Stories vinculadas

- [ST-034 - Webmail Admin E2E (SMTP/POP3 Gmail) com Portal + Mobile + Push](../STORIES/IN_PROGRESS/ST-034-webmail-admin-e2e-smtp-pop3.md)
