# EPIC-003 - Notificacoes administrativas para acessos e captacao da landing

Status: DONE
Trilha: LANDING_PAGE, API, ADMIN

## Objetivo

Dar visibilidade operacional imediata ao time administrativo sempre que houver acesso publico relevante na landing e sempre que um lead comercial for captado na home publica.

## Problema de negocio

- A landing publica gera visitas e conversoes sem acionar o time admin em tempo real.
- O app admin e o portal admin ja possuem infraestrutura de push/notificacao, mas a landing ainda nao publica eventos nesse barramento.
- O time perde timing comercial para abordar clientes interessados e prestadores parceiros logo apos o primeiro contato.

## Resultado esperado

- Cada acesso a `https://www.consertapramim.com`, `https://www.consertapramim.com/Cliente` e `https://www.consertapramim.com/Prestador` gera notificacao administrativa com metadados tecnicos essenciais.
- Cada lead `Cliente` ou `Prestador` captado na landing gera notificacao administrativa em tempo real.
- O envio reutiliza a infraestrutura ja existente de notificacoes do ecossistema, chegando ao portal admin em tempo real e ao app admin quando houver device registrado.
- A integracao da landing com a API interna usa token de webhook e nao expoe endpoint novo no Swagger publico.

## Metricas de sucesso

- tempo medio entre acesso/lead na landing e recebimento do push no admin;
- percentual de acessos da landing que geram notificacao valida sem degradar o carregamento da home;
- percentual de leads captados com push administrativo emitido com sucesso;
- tempo de reacao comercial apos lead captado.

## Escopo

### Inclui

- webhook interno da landing para publicar evento de acesso;
- servico na API para fan-out das notificacoes para admins ativos;
- notificacao administrativa para leads `Cliente` e `Prestador` captados na landing;
- atualizacao de compose/configuracao da landing para falar com a API interna;
- manual QA/Operacao, Story, indice, changelog e diagrama Mermaid.

### Nao inclui

- persistencia de analytics de acessos da landing em tabela dedicada;
- dashboard administrativo novo para trafego da landing;
- automacao comercial por e-mail, WhatsApp ou CRM externo.

## Historias vinculadas

- ST-003 - Push admin para acesso publico e lead captado na landing.
