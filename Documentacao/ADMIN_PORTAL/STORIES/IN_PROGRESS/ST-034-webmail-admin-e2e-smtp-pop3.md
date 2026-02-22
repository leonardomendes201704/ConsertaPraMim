# ST-034 - Webmail Admin E2E (SMTP/POP3 Gmail) com Portal + Mobile + Push

Status: In Progress
Epic: EPIC-012

## Objetivo

Entregar um fluxo de webmail administrativo ponta a ponta para operacao do ConsertaPraMim, permitindo que admins enviem e acompanhem emails para clientes e prestadores, com suporte inicial a SMTP/POP3 Gmail, notificacoes em tempo real no portal e push/toast no app admin mobile.

## Escopo funcional

- Admin configura credenciais SMTP/POP3 e parametros de conexao.
- Admin envia email para cliente/prestador (compose).
- Admin visualiza inbox/sent no portal admin.
- App admin mobile visualiza inbox e permite compose basico.
- Job de sincronizacao POP3 busca emails recebidos e atualiza inbox.
- Novos emails recebidos geram notificacao realtime (portal) e push/toast (mobile admin).

## Criterios de aceite

- Existe backend admin para:
  - salvar/consultar configuracao de mailbox (SMTP/POP3),
  - enviar email via SMTP,
  - sincronizar e listar inbox via POP3,
  - obter detalhe de mensagem e marcar como lida.
- Apenas usuarios admin podem acessar endpoints e telas do webmail.
- Portal admin possui nova area "Webmail" no menu lateral com:
  - inbox,
  - compose,
  - configuracao SMTP/POP3.
- App admin mobile possui nova aba "Webmail" com inbox + compose.
- Sincronizacao POP3 em background nao derruba API em falhas de autenticao/rede.
- Entrada de novo email gera notificacao para admins com payload tipado (ex.: `admin_event_inbound_email`).
- Logs operacionais/auditoria registram alteracoes de configuracao e envio de email.
- Documentacao operacional inclui prerequisitos Gmail (App Password, POP habilitado e SMTP ativo).

## Tasks

- [ ] Task 1 - Modelagem e documentacao:
  - Criar historia tecnica ST-034 com plano de entrega em fases.
  - Definir contratos DTO/servicos para mailbox admin.

- [ ] Task 2 - Backend core webmail:
  - Implementar servico de mailbox admin (config, inbox, send, read/unread).
  - Implementar gateway SMTP/POP3 (MailKit) para Gmail.
  - Expor endpoints `/api/admin/mailbox/*` protegidos por role admin.
  - Implementar worker de sync POP3 periodico.
  - Disparar notificacao admin (signalr + push) para novo email inbound.

- [ ] Task 3 - Portal admin webmail:
  - Criar controller/view "AdminMailbox" com inbox, compose e settings.
  - Adicionar item "Webmail" no menu lateral.
  - Integrar chamadas backend com autenticacao admin.

- [ ] Task 4 - App admin mobile webmail:
  - Criar aba "Webmail" com inbox e compose.
  - Integrar consumo dos endpoints `/api/admin/mailbox/*`.
  - Exibir feedback de envio/sync e estado de carregamento/erro.

- [ ] Task 5 - Validacao e hardening:
  - Build backend/API e frontend mobile admin.
  - Validar fluxo E2E:
    1) salvar credenciais,
    2) enviar email para cliente/prestador,
    3) receber resposta no inbox,
    4) receber push/toast no mobile admin.
  - Atualizar runbook de operacao do webmail.

## Plano curto de implementacao

1. Backend primeiro (contrato estavel para portal e mobile).
2. Portal admin webmail (operacao principal).
3. Mobile admin webmail (monitoramento rapido e envio basico).
4. Ajustes finais de UX, logs e documentacao operacional.

