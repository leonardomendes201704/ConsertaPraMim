# EPIC-021 - AI Copilot para growth funnel e score de liquidez

Status: In Progress
Trilha: GROWTH_GOVERNANCA

## Objetivo

Adicionar um copiloto de IA no ecossistema admin para analisar dados de `Growth Funnel` e `Score de Liquidez`, gerar diagnostico executivo e recomendar acoes operacionais priorizadas.

## Problema de negocio

- Lideranca precisa consolidar rapidamente sinais de gargalo (proposta/aceite/SLA) e deficit de liquidez por regiao/categoria.
- Leitura manual dos paineis reduz velocidade de decisao semanal e de resposta operacional.
- Falta um mecanismo padronizado para transformar KPI em plano de acao objetivo.

## Resultado esperado

- Configuracao administravel da OpenAI API key/modelo no portal admin, com persistencia segura em runtime.
- Analise assistida de funil + liquidez com resumo executivo, riscos e acoes recomendadas.
- Historico de analises para rastreabilidade do contexto de decisao.

## Metricas de sucesso

- Tempo medio para gerar diagnostico executivo apos atualizacao de filtros (meta: < 30s).
- % de rodadas de governanca com uso de analise assistida registrada.
- Reducao de tempo entre deteccao de gargalo e definicao de acao operacional.

## Escopo

### Inclui

- API admin para configurar copiloto IA e executar analise.
- Integração OpenAI (Responses API) para gerar recomendacoes textuais estruturadas.
- Tela no portal admin para configurar chave/modelo e disparar analise.
- Consumo dos dados de `Growth Funnel` + `Liquidity Score` na mesma rodada de IA.
- Atualizacoes de manual QA/operacao e changelog.

### Nao inclui

- Execucao automatica de acoes de negocio sem aprovacao humana.
- Treinamento de modelo customizado.
- Exposicao da API key em clientes mobile/web finais.

## Historias vinculadas

- ST-052 - AI Copilot no portal admin para diagnostico de growth funnel e liquidez.
