# ST-107 - Notificacao confiavel para prestadores com links assinados

Status: Done localmente
Epic: EPIC-JORNADA-001

## Objetivo

Garantir que a oportunidade chegue ao prestador mesmo quando ele usa bot em outros canais, capturando o aceite por mecanismo controlado.

## Criterios de aceite

- Toda oportunidade gera email com CTA assinado.
- A resposta do prestador nao depende de parsing de texto.
- Existe rastreio de envio, abertura, clique e aceite.
- O aceite e a recusa oficiais acontecem via link assinado ou portal/app.

## Tasks

- [x] Criar templates de email com `Aceitar` e `Recusar`.
- [x] Gerar links assinados, expiraveis e idempotentes.
- [x] Criar endpoint de aceite e recusa autenticado por token assinado.
- [x] Registrar telemetria de envio, abertura e clique.
- [x] Definir canal complementar opcional sem depender dele para aceite.
- [x] Cobrir link expirado, clique repetido e target ja reservado.

## Entrega implementada

- O `JourneyProviderDispatchNotificationService` passou a gerar o e-mail HTML da oportunidade com CTA de `Aceitar` e `Recusar`, operando por `log` ou `smtp`.
- O `JourneyProviderDispatchLinkService` passou a assinar os links com HMAC, expiracao automatica e validacao de proposito.
- O `JourneyProviderOpportunityController` e o `JourneyProviderOpportunityService` passaram a expor a pagina segura `/prestadores/oportunidades/responder` e o pixel `/prestadores/oportunidades/rastreio-abertura`.
- O aceite oficial do prestador agora acontece no `POST` da pagina segura, sem depender de parsing de texto em outros canais.
- A recusa oficial registra telemetria, muda o alvo para `Recusado` e pode liberar imediatamente a proxima onda elegivel.
- O snapshot do disparo passou a persistir tentativas, status de entrega, aberturas, cliques, ultima interacao e ultimo erro por alvo.
- O modal do lead no Kanban passou a exibir essa telemetria dentro da secao `Disparo em ondas`.
- Foram adicionados testes para o link assinado, para a resposta do prestador e para a integracao do dispatch com o notificador.

## Validacoes

- `dotnet build Backend\src\ConsertaPraMim.Web.CpmFull\ConsertaPraMim.Web.CpmFull.csproj -p:UseAppHost=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st107-cpm-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st107-cpm-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st107-cpm-out\ -p:DefaultItemExcludes=obj/**`
- `dotnet test Backend\tests\ConsertaPraMim.Tests.Unit\ConsertaPraMim.Tests.Unit.csproj --filter "(FullyQualifiedName~JourneyProviderDispatchServiceTests|FullyQualifiedName~JourneyProviderDispatchLinkServiceTests|FullyQualifiedName~JourneyProviderOpportunityServiceTests)" -p:UseAppHost=false -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath='C:\Users\devcr\AppData\Local\Temp\codex-st107-test-obj\$(MSBuildProjectName)\' -p:MSBuildProjectExtensionsPath='C:\Users\devcr\AppData\Local\Temp\codex-st107-test-obj\$(MSBuildProjectName)\' -p:OutputPath='C:\Users\devcr\AppData\Local\Temp\codex-st107-test-out\$(MSBuildProjectName)\' -p:DefaultItemExcludes=obj/** --logger "console;verbosity=minimal"`
- `git diff --check`
- varredura de encoding UTF-8 nos arquivos alterados

## Risco / Impacto

Medio. A entrega muda a forma oficial de aceite/recusa da oportunidade do prestador e passa a depender de URL publica valida, segredo de assinatura e canal de e-mail operacional.
