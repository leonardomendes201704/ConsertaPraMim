# Manual QA/Operacao - Fundacao Google Calendar Sync (ST-011)

## 1. Objetivo

Padronizar configuracao, validacao e smoke test da integracao Google Calendar via Service Account para a trilha de agendamentos.

## 2. Escopo da ST-011

- Fundacao tecnica da integracao (`IGoogleCalendarService`).
- Validacao de startup para configuracoes obrigatorias quando integracao estiver habilitada.
- Conversao de janela UTC para timezone de negocio (`America/Sao_Paulo`) no payload enviado ao Google.
- Template de evento com titulo, descricao, local e metadados operacionais.

## 3. Pre-requisitos

- Projeto Google Cloud com `Google Calendar API` habilitada.
- Service Account criada no projeto Google.
- Chave privada da Service Account provisionada em secret manager/local secret.
- Calendario unico da operacao compartilhado com permissao de edicao para o e-mail da Service Account.

## 4. Configuracao da API

Seção `GoogleCalendarSync` em `ConsertaPraMim.API`:

```json
{
  "GoogleCalendarSync": {
    "Enabled": true,
    "ProjectId": "consertapramim-prod",
    "ServiceAccountEmail": "agenda-sync@consertapramim-prod.iam.gserviceaccount.com",
    "PrivateKey": "__SET_VIA_SECRET__",
    "CalendarId": "seu-calendario@group.calendar.google.com",
    "Timezone": "America/Sao_Paulo"
  }
}
```

Regras:
- Se `Enabled=false`, integracao fica desativada e nao bloqueia startup.
- Se `Enabled=true`, todos os campos obrigatorios precisam estar preenchidos.

## 5. Checklist QA (smoke)

- [ ] QA-GCAL-001: API sobe com `Enabled=false` e sem credenciais.
- [ ] QA-GCAL-002: API falha no startup com `Enabled=true` e `ProjectId` ausente.
- [ ] QA-GCAL-003: API falha no startup com `Enabled=true` e `CalendarId` ausente.
- [ ] QA-GCAL-004: API falha no startup com `Enabled=true` e timezone invalido.
- [ ] QA-GCAL-005: build da API conclui sem erro com pacote `Google.Apis.Calendar.v3`.
- [ ] QA-GCAL-006: testes unitarios `GoogleCalendar*` aprovados.

## 6. Procedimento operacional (Service Account + calendario unico)

1. Abrir Google Cloud Console e selecionar/criar projeto da integracao.
2. Ativar a API `Google Calendar API`.
3. Criar Service Account dedicada da integracao.
4. Gerar chave JSON da Service Account e armazenar em cofre de segredos.
5. Abrir Google Calendar e compartilhar o calendario unico com o e-mail da Service Account.
6. Conceder permissao `Make changes to events`.
7. Configurar `GoogleCalendarSync` na API via secrets/variaveis de ambiente.
8. Subir API e validar startup.

## 7. Troubleshooting

## 7.1 Startup bloqueado por validacao

- Conferir `GoogleCalendarSync:Enabled`.
- Se estiver `true`, validar se todos os campos obrigatorios foram informados.
- Confirmar formato do `Timezone` (`America/Sao_Paulo` recomendado).

## 7.2 Falha de autenticacao no Google

- Validar e-mail da Service Account.
- Confirmar chave privada correta (incluindo quebra de linha).
- Confirmar permissao de edicao no calendario compartilhado.

## 7.3 Evento criado com horario incorreto

- Verificar se janela de agendamento foi persistida em UTC no sistema.
- Verificar `GoogleCalendarSync:Timezone` configurado para `America/Sao_Paulo`.
