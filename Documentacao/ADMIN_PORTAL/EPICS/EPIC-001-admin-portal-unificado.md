# EPIC-001 - Portal Admin Unificado

## Objetivo

Entregar um portal dedicado para administracao da plataforma, com visao global e operacao segura de usuarios, pedidos, propostas, conversas e notificacoes.

## Problema atual

- O admin atual esta embutido no portal do prestador.
- A cobertura funcional de administracao e limitada.
- Falta trilha de auditoria e governanca operacional.

## Resultado esperado

- Projeto `ConsertaPraMim.Web.Admin` separado.
- Endpoints administrativos dedicados na API.
- Dashboard operacional com dados em tempo real.
- Acoes administrativas auditadas.
- Processo de rollout com QA e changelog.

## Metricas de sucesso

- 100% das rotas admin protegidas por role/policy de Admin.
- 0 possibilidade de criar usuario Admin via registro publico.
- Dashboard com principais KPIs em menos de 2s na carga inicial.
- Toda acao sensivel com trilha de auditoria persistida.

## Escopo

### Inclui

- Seguranca e hardening de acesso admin.
- Novo portal web admin.
- API admin para leitura e operacao.
- Monitoramento de usuarios, pedidos, propostas, chats e notificacoes.
- Auditoria, testes e rollout.

### Nao inclui

- BI externo completo (Data Warehouse).
- Aplicativo mobile de administracao.
- Automacoes de ML/fraude nesta fase.

## Historias vinculadas

- ST-001 ate ST-010 (ver `../INDEX.md`).

