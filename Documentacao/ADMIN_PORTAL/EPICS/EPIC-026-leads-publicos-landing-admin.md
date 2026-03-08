# EPIC-026 - Leads publicos da landing no Portal Admin

Status: Done
Trilha: ADMIN_PORTAL, LANDING_PAGE, API

## Objetivo

Dar visibilidade operacional aos leads captados pela landing publica, permitindo que o time admin acompanhe volume, origem, localidade e metadados tecnicos dos contatos recebidos sem depender de consulta direta ao banco.

## Problema de negocio

- A landing publica passou a captar leads de clientes e prestadores, mas a operacao ainda nao possui uma tela administrativa dedicada para triagem e acompanhamento desses contatos.
- A equipe perde tempo consultando banco/logs para validar localidade, UTM, device e contexto tecnico de navegacao de um lead.
- Sem um modulo admin, nao existe rastreabilidade operacional simples para follow-up comercial, auditoria e QA do fluxo de captacao.

## Resultado esperado

- O portal admin ganha um item de menu `Leads Landing`.
- O admin visualiza uma grade com filtros em drawer offcanvas para origem, busca, cidade, UF e periodo.
- O admin pode abrir a tela de detalhe de um lead e consultar todos os dados comerciais e tecnicos capturados pela landing.
- A API expoe endpoints administrativos autenticados para listagem e detalhe dos leads.
- Documentacao, changelog, manual QA e diagrama Mermaid saem no mesmo ciclo.

## Metricas de sucesso

- Tempo reduzido para localizar um lead especifico na operacao.
- Visibilidade de volume captado por origem (`Cliente` x `Prestador`) diretamente no admin.
- Menor dependencia de acesso manual ao banco para auditoria de captacao.

## Escopo

### Inclui

- modulo administrativo de leads da landing;
- grid com filtros em drawer;
- tela de detalhe do lead;
- endpoints admin autenticados para consulta;
- atualizacao de menu, manual QA, changelog e diagrama.

### Nao inclui

- CRM completo de pipeline comercial;
- atribuicao de lead para operador;
- automacao de notificacao/email/WhatsApp apos captura;
- dashboard executivo dedicado de conversao de leads.

## Historias vinculadas

- ST-058 - Gestao administrativa de leads captados pela landing.
