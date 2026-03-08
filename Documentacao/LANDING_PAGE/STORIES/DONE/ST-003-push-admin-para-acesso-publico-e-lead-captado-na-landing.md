# ST-003 - Push admin para acesso publico e lead captado na landing

Status: DONE
Epic: EPIC-003

## Objetivo

Publicar eventos operacionais da landing no barramento administrativo, notificando admins ativos quando houver novo acesso publico na home e quando um lead comercial for captado.

## Criterios de aceite

- Aberturas da landing em `/`, `/Cliente` e `/Prestador` disparam notificacao administrativa com path, IP, user-agent e dados basicos de contexto.
- O envio do push de acesso nao pode quebrar nem bloquear o carregamento da landing se a API interna estiver indisponivel.
- A captura de lead `Cliente` e `Prestador` continua persistindo em banco e agora dispara notificacao administrativa com contexto comercial e localidade.
- A implementacao reutiliza a infraestrutura ja existente de notificacao admin/web/mobile, sem criar canal paralelo.
- O endpoint interno de acesso da landing nao aparece no Swagger publico.
- Compose/configuracao da landing recebem URL interna da API + token de webhook.
- Manual QA/Operacao, changelog, indice e diagrama Mermaid atualizados no mesmo ciclo.

## Tasks concluidas

- [x] Registrar Epic/Story/indice/diagrama da trilha de notificacoes da landing.
- [x] Implementar servico de notificacao admin para fan-out de acessos e leads da landing.
- [x] Criar endpoint interno da API para eventos de acesso da landing com token de webhook.
- [x] Integrar a landing ao webhook interno com timeout curto e fallback seguro.
- [x] Disparar notificacao administrativa apos captura de lead `Cliente` ou `Prestador`.
- [x] Cobrir com testes de regressao e atualizar manual/changelog para operacao.

## Escopo entregue

- a landing passou a publicar acessos de `/`, `/Cliente` e `/Prestador` em um webhook interno autenticado por `X-Deploy-Token`;
- a API passou a fan-out esses eventos para admins ativos usando o barramento existente de notificacoes, cobrindo portal admin em tempo real e app admin quando houver device registrado;
- a captura de leads da landing agora dispara notificacao administrativa apos persistencia bem-sucedida;
- a landing recebeu configuracao dedicada de `InternalApiBaseUrl` e `InternalWebhookToken` para publicar eventos sem depender da URL publica;
- o endpoint interno ficou fora do Swagger publico com `ApiExplorerSettings(IgnoreApi = true)`;
- documentacao operacional, changelog e diagrama Mermaid foram atualizados para rollout e suporte.

## Validacao esperada

- carregar `https://www.consertapramim.com` gera notificacao admin `Novo acesso na landing`;
- carregar `https://www.consertapramim.com/Cliente` ou `/Prestador` gera notificacao com path correspondente;
- enviar lead `Cliente` gera notificacao `Novo lead de cliente na landing` com link para o detalhe no admin;
- enviar lead `Prestador` gera notificacao `Novo lead de prestador na landing` com link para o detalhe no admin;
- indisponibilidade do webhook interno nao derruba a landing nem impede a renderizacao da home;
- o endpoint `POST /api/internal/landing/access` nao aparece no Swagger publico.
