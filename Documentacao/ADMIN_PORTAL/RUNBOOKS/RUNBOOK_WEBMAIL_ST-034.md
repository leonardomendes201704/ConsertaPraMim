# RUNBOOK - Webmail Admin (ST-034)

## Objetivo

Padronizar o setup e a operacao do modulo de Webmail Admin (portal + app mobile), com Gmail SMTP/POP3 e notificacoes para novos emails inbound.

## Pre-requisitos

1. Conta Gmail operacional para envio/recebimento.
2. Verificacao em 2 etapas habilitada na conta Gmail.
3. App Password criada no Gmail (16 caracteres).
4. POP habilitado no Gmail:
   - Gmail > Settings > See all settings > Forwarding and POP/IMAP > Enable POP for all mail.
5. API em deploy com os endpoints `/api/admin/mailbox/*`.
6. Push Firebase ativo na API (para push mobile admin):
   - `PushNotifications__Firebase__ProjectId`
   - `PushNotifications__Firebase__ServiceAccountJsonBase64`

## Configuracao inicial (Portal Admin)

1. Acessar menu `Webmail` no portal admin.
2. Em `Configuracao SMTP/POP3`, preencher:
   - `Nome do remetente`
   - `Email remetente`
   - `Usuario (Gmail)`
   - `Senha/App Password`
   - `SMTP Host`: `smtp.gmail.com`
   - `SMTP Porta`: `587` (TLS) ou `465` (SSL)
   - `SMTP SSL/TLS`: habilitado
   - `POP3 Host`: `pop.gmail.com`
   - `POP3 Porta`: `995`
   - `POP3 SSL/TLS`: habilitado
   - `Janela de sync`: recomendado `40`
   - `Intervalo polling`: recomendado `120`
3. Salvar configuracao.
4. Clicar `Sincronizar inbox` para validar conectividade imediata.

## Fluxo E2E de validacao

1. Portal admin:
   - enviar email para um cliente/prestador.
   - validar email em `Enviados`.
2. Responder o email no destino (cliente/prestador) para a conta Gmail configurada.
3. Portal admin:
   - sincronizar inbox ou aguardar worker.
   - validar email em `Inbox`.
4. App admin mobile:
   - abrir aba `Webmail`.
   - validar email inbound na lista.
   - abrir detalhe e marcar como lido/nao lido.
5. Notificacoes:
   - validar toast no portal admin e push/toast no app admin quando chegar inbound.

## Operacao diaria

1. Monitorar `Last sync`, `Last sync status` e `Last sync error`.
2. Usar busca por assunto/remetente para localizar conversas.
3. Manter a conta Gmail dedicada ao operacional (evitar uso pessoal).
4. Trocar App Password periodicamente e atualizar no portal.

## Troubleshooting

## Erro de autenticacao SMTP/POP3

- Sintoma: erro ao salvar/sincronizar/enviar.
- Checklist:
  1. confirmar App Password valida (nao usar senha normal da conta).
  2. confirmar 2FA ativa.
  3. confirmar POP habilitado na conta.
  4. testar portas/hosts padrao do Gmail.

## Inbox nao atualiza no app admin mobile

- Sintoma: portal recebe, app nao mostra.
- Checklist:
  1. abrir aba `Webmail` e clicar `Sincronizar`.
  2. validar token admin ainda ativo (logout/login).
  3. conferir erro em resposta da API no device log.

## Push nao chega no mobile admin

- Sintoma: inbound aparece no portal mas sem push no app.
- Checklist:
  1. confirmar registro de device `AppKind=admin` em `MobilePushDevices`.
  2. validar variaveis Firebase no container da API.
  3. revisar logs da API buscando `admin_event_inbound_email` e falhas de push.

## Variaveis de ambiente recomendadas (API)

- `AdminMailbox__SyncWorker__Enabled=true`
- `AdminMailbox__SyncWorker__IntervalSeconds=120`

## Observacao de seguranca

- Nao versionar App Password em codigo/repositorio.
- Limitar acesso ao menu `Webmail` a usuarios `Admin`.
- Rotacionar App Password em incidentes de credencial.
