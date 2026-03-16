# ST-113 - Hotfix de qualificacao do Telegram com pergunta unica e sem reset

## Contexto

Durante os testes reais da jornada automatica no Telegram, o bot ainda apresentava dois desvios de comportamento:

1. fazia perguntas de qualificacao com varios campos ao mesmo tempo, o que piorava a taxa de resposta;
2. depois que o lead ja estava qualificado e o usuario enviava apenas o e-mail, o fluxo voltava para o prompt inicial e pedia telefone novamente.

## Objetivo

Corrigir a experiencia conversacional do bot para:

- pedir um unico dado por vez;
- aproveitar respostas curtas de localizacao;
- reconhecer a captura isolada de e-mail sem reiniciar a conversa;
- manter a qualificacao guiada pelos campos faltantes do CPM Full.

## Criterios de aceite

1. Depois que o telefone for informado, o bot pergunta apenas o primeiro campo obrigatorio faltante.
2. Respostas curtas como `Praia Grande` continuam a qualificacao.
3. Quando o lead ja estiver qualificado e o usuario enviar apenas o e-mail, o bot reconhece o e-mail e nao volta ao prompt inicial.
4. O fluxo continua sem handoff humano em cenarios normais de triagem.
5. Existe regressao automatizada para o cenario de e-mail sem reset.

## Entrega implementada

- O `TelegramInboundUpdateProcessor` passou a montar a proxima pergunta com base apenas no primeiro item de `MissingRequiredFields`.
- O prompt inicial agora so aparece em lead realmente novo sem telefone persistido; conversas existentes nao retornam para a mensagem de bootstrap.
- O reconhecimento de e-mail no fim da qualificacao passou a responder com confirmacao objetiva e sem pedir telefone novamente.
- Os testes unitarios do Telegram foram ajustados para a estrategia de pergunta unica e ganharam cobertura para o cenario de e-mail sem reset.

## Validacao executada

- `dotnet build Backend\src\ConsertaPraMim.Web.TelegramBridge\ConsertaPraMim.Web.TelegramBridge.csproj -p:UseAppHost=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st113-bridge-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st113-bridge-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st113-bridge-out\ -p:DefaultItemExcludes=obj/**`
- `dotnet build Backend\src\ConsertaPraMim.Web.CpmFull\ConsertaPraMim.Web.CpmFull.csproj -p:UseAppHost=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st113-cpm-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st113-cpm-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st113-cpm-out\ -p:DefaultItemExcludes=obj/**`
- `dotnet test Backend\tests\ConsertaPraMim.Tests.Unit\ConsertaPraMim.Tests.Unit.csproj --filter "(FullyQualifiedName~TelegramInboundUpdateProcessorTests|FullyQualifiedName~TelegramLeadAutomationServiceTests)" -p:UseAppHost=false -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st113-test-obj\ -p:MSBuildProjectExtensionsPath=C:\Users\devcr\AppData\Local\Temp\codex-st113-test-obj\ -p:OutputPath=C:\Users\devcr\AppData\Local\Temp\codex-st113-test-out\ -p:DefaultItemExcludes=obj/** --logger "console;verbosity=minimal"`

## Risco/impacto

- Medio. A mudanca altera diretamente a experiencia conversacional do Telegram e a forma como a triagem automatica conduz a coleta incremental dos dados.
