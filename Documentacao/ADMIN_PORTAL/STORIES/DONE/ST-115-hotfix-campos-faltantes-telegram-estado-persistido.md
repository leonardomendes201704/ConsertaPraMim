# ST-115 - Hotfix para campos faltantes do Telegram baseados no estado persistido

## Contexto

Durante os testes reais da jornada no Telegram, o fluxo ainda regressava para `tipo de servico` depois que o usuario informava o `CEP`, mesmo com a categoria ja salva anteriormente.

## Objetivo

Fazer a triagem do Telegram decidir o proximo campo faltante com base no estado persistido da jornada no CPM Full, e nao apenas na mensagem atual.

## Criterios de aceite

1. Se `categoria` ja estiver persistida, ela nao pode voltar para a lista de faltantes.
2. Se `CEP` ja estiver persistido, ele nao pode voltar para a lista de faltantes.
3. O fluxo `telefone -> categoria -> CEP` deve seguir para o proximo campo coerente.
4. Existe regressao automatizada para o caso `categoria persistida + CEP informado`.

## Entrega implementada

- O contrato interno entre `TelegramBridge` e `CPM Full` passou a devolver tambem `HasPostalCode`, `HasAddressDetails` e `HasProblemContext`.
- O `TelegramInboundUpdateProcessor` passou a filtrar `MissingRequiredFields` usando o estado persistido do lead/jornada, incluindo `telefone`, `categoria`, `cidade`, `CEP`, `logradouro/bairro` e `contexto do problema`.
- Foi adicionada regressao para validar que, apos informar `eletricista` e depois `11704150`, o bot segue para `cidade` em vez de voltar para `tipo de servico`.

## Validacao executada

- `dotnet build Backend\src\ConsertaPraMim.Web.TelegramBridge\ConsertaPraMim.Web.TelegramBridge.csproj -p:UseAppHost=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st115-bridge-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st115-bridge-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st115-bridge-out\ -p:DefaultItemExcludes=obj/**`
- `dotnet build Backend\src\ConsertaPraMim.Web.CpmFull\ConsertaPraMim.Web.CpmFull.csproj -p:UseAppHost=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st115-cpm-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st115-cpm-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st115-cpm-out\ -p:DefaultItemExcludes=obj/**`
- `dotnet test Backend\tests\ConsertaPraMim.Tests.Unit\ConsertaPraMim.Tests.Unit.csproj --filter "(FullyQualifiedName~TelegramInboundUpdateProcessorTests|FullyQualifiedName~TelegramLeadAutomationServiceTests)" -p:UseAppHost=false -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st115-test-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st115-test-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st115-test-out\ -p:DefaultItemExcludes=obj/** --logger "console;verbosity=minimal"`

## Risco/impacto

- Medio. A correcao altera o contrato interno entre bridge e CPM Full e o calculo da proxima pergunta da triagem automatica no Telegram.
