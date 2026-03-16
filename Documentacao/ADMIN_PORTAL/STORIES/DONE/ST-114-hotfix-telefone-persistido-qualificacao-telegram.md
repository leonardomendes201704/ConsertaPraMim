# ST-114 - Hotfix para preservar telefone persistido na qualificacao do Telegram

## Contexto

Mesmo apos o hotfix de pergunta unica, o bot Telegram ainda entrava em loop quando o telefone ja tinha sido salvo no lead e a proxima mensagem trazia apenas categoria ou cidade.

## Objetivo

Garantir que o bridge reconheca `telefone persistido` como contato valido ao montar a proxima pergunta da triagem.

## Criterios de aceite

1. Depois de o usuario informar o telefone, a proxima mensagem nao pode voltar a pedir telefone.
2. Se o usuario responder `eletricista`, o bot deve avancar para o proximo campo faltante, como `cidade`.
3. O estado persistido do lead deve ser aproveitado na composicao do prompt.
4. Existe teste de regressao para o cenario `telefone persistido + categoria informada`.

## Entrega implementada

- O `TelegramInboundUpdateProcessor` passou a montar um `capturedContact` efetivo com placeholders de estado persistido quando `HasPhoneOnLead` ou `HasEmailOnLead` vierem do CPM Full.
- A normalizacao dos campos faltantes deixou de reintroduzir `Telefone` quando ele ja esta salvo no lead, ainda que a mensagem atual nao o contenha.
- Foi adicionada regressao para validar que, apos `telefone -> eletricista`, o bot segue para `cidade` em vez de voltar a `telefone`.

## Validacao executada

- `dotnet build Backend\src\ConsertaPraMim.Web.TelegramBridge\ConsertaPraMim.Web.TelegramBridge.csproj -p:UseAppHost=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st113b-bridge-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st113b-bridge-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st113b-bridge-out\ -p:DefaultItemExcludes=obj/**`
- `dotnet test Backend\tests\ConsertaPraMim.Tests.Unit\ConsertaPraMim.Tests.Unit.csproj --filter "(FullyQualifiedName~TelegramInboundUpdateProcessorTests|FullyQualifiedName~TelegramLeadAutomationServiceTests)" -p:UseAppHost=false -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st113b-test-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st113b-test-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st113b-test-out\ -p:DefaultItemExcludes=obj/** --logger "console;verbosity=minimal"`

## Risco/impacto

- Medio. A correcao altera diretamente o estado conversacional do Telegram e o aproveitamento dos dados persistidos da jornada.
