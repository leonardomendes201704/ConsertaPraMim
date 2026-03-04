# ST-011 - Fundacao Google Calendar com Service Account e calendario unico

Status: Backlog  
Epic: EPIC-003

## Objetivo

Preparar a base tecnica e operacional para integrar a API com Google Calendar usando Service Account e um calendario unico da operacao.

## Criterios de aceite

- API consegue autenticar no Google Calendar com Service Account.
- Calendario unico de operacao esta compartilhado com permissao de edicao para a Service Account.
- Configuracoes sensiveis sao carregadas por ambiente sem hardcode.
- Existe cliente de integracao isolado com contrato claro para `create/update/delete`.
- Manual operacional descreve setup completo de credenciais e permissoes.

## Tasks

- [ ] Criar options de configuracao para Google Calendar (`ProjectId`, `ServiceAccountEmail`, `PrivateKey`, `CalendarId`, `Timezone`).
- [ ] Implementar validacao de startup para impedir execucao sem configuracao obrigatoria.
- [ ] Adicionar pacote oficial `Google.Apis.Calendar.v3` na API.
- [ ] Implementar cliente `IGoogleCalendarService` com metodos basicos de evento.
- [ ] Definir payload padrao do evento (titulo, descricao, local, metadados operacionais).
- [ ] Persistir timezone de negocio (`America/Sao_Paulo`) com armazenamento UTC no sistema.
- [ ] Criar manual passo a passo de onboarding da Service Account e compartilhamento do calendario.

## Passo a passo operacional (Service Account + calendario unico)

1. Criar projeto no Google Cloud Console para a integracao.
2. Habilitar a API `Google Calendar API` no projeto.
3. Criar uma `Service Account` dedicada (ex.: `agenda-consertapramim@...`).
4. Gerar chave JSON da Service Account e armazenar em cofre seguro.
5. No Google Calendar, criar ou selecionar o calendario unico da operacao.
6. Compartilhar o calendario com o e-mail da Service Account com permissao `Make changes to events`.
7. Configurar `CalendarId` real no `appsettings`/secrets por ambiente.
8. Validar com teste de smoke: criar evento tecnico e remover em seguida.

