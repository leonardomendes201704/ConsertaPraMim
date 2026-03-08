# EPIC-027 - KPIs da landing no dashboard admin

Status: Done
Trilha: ADMIN_PORTAL, LANDING_PAGE, API

## Objetivo

Dar visibilidade executiva para o funil inicial da landing publica diretamente na home do portal admin, consolidando visitas, cadastros por origem e taxa de conversao no mesmo recorte temporal ja usado pelos demais KPIs operacionais.

## Problema de negocio

- A landing publica ja captura acessos e leads, mas o dashboard admin ainda nao transforma esse volume em KPI de acompanhamento.
- A operacao nao consegue responder rapidamente quantas visitas a landing recebeu, quantos cadastros vieram de `Cliente` e `Prestador` e qual foi a taxa de conversao no periodo.
- Sem persistencia historica dos acessos, os eventos enviados hoje por notificacao nao sustentam analise gerencial nem comparacao entre periodos.

## Resultado esperado

- Cada acesso relevante da landing passa a ser persistido em tabela historica dedicada.
- A home admin exibe KPIs incrementais para:
  - `Visitas`
  - `Cadastros Prestador`
  - `Cadastros Cliente`
  - `Taxa de Conversao`
- O KPI `Visitas` detalha visitantes unicos e visitantes recorrentes no mesmo recorte.
- A taxa de conversao usa a relacao entre visitas e cadastros captados na landing no periodo filtrado.
- O fluxo usa `visitorId` estavel para suportar analise de visitantes unicos e visitantes convertidos.
- Story, changelog, manual QA/Operacao e diagrama Mermaid saem no mesmo ciclo.

## Metricas de sucesso

- Time admin consegue verificar o volume de visitas e cadastros da landing sem sair da home.
- KPI de conversao passa a orientar analise comercial do topo do funil.
- Operacao reduz dependencia de logs/notificacoes para responder desempenho da landing por periodo.

## Escopo

### Inclui

- persistencia historica de acessos da landing;
- `visitorId` em acessos e leads para correlacao de conversao;
- agregacao dos dados no dashboard admin;
- 4 novos KPIs incrementais na home admin;
- atualizacao de manual, changelog, indices e diagrama.

### Nao inclui

- atribuicao de leads para operador;
- dashboard dedicado exclusivo de marketing;
- integracao com analytics externo;
- cohort por campanha ou dashboard por UTM.

## Historias vinculadas

- ST-059 - KPIs de visitas, cadastros e conversao da landing na home admin.
