# ST-052 - AI Copilot no portal admin para diagnostico de growth funnel e liquidez

Status: In Progress
Epic: EPIC-021

## Objetivo

Permitir que o admin gere analises assistidas por IA, com contexto de funil e liquidez, para acelerar decisao semanal de growth e resposta operacional.

## Criterios de aceite

- Existe configuracao no portal admin para OpenAI API key/modelo, persistida no backend sem expor segredo em texto aberto na UI.
- API admin disponibiliza endpoint para disparar analise usando dados reais de `Growth Funnel` e `Score de Liquidez`.
- Resultado retorna resumo executivo, insights de funil, insights de liquidez, riscos e plano de acao.
- Portal admin exibe historico recente das analises com filtros aplicados e metadados de execucao.
- Swagger documenta endpoints novos com contexto de negocio e tecnico.
- Manual QA/Operacao cobre configuracao, execucao e troubleshooting do modulo.

## Tasks

- [x] Abrir epic/story/tasks e registrar backlog de entrega incremental.
- [ ] Implementar backend (store de configuracao IA + servico de analise + endpoints admin).
- [ ] Implementar portal admin (menu, tela de configuracao e disparo de analise IA).
- [ ] Integrar leitura do cockpit growth/liquidez no prompt de analise e persistir historico.
- [ ] Cobrir com testes, manual QA/Operacao, changelog e fechamento da story.

## Plano curto (arquitetura + passos)

1. Persistencia e configuracao:
- usar `SystemSettings` para snapshot do modulo (`settings + historico`);
- API key atualizavel com mascara no retorno.

2. Motor de analise:
- coletar dados de `AdminGrowthService` e `AdminLiquidityScoreService`;
- montar prompt contextualizado;
- chamar OpenAI Responses API;
- extrair output estruturado e persistir.

3. Portal admin:
- nova tela `AI Copilot Growth` com bloco de configuracao e bloco de analise;
- mostrar ultima execucao e historico recente.

4. Governanca:
- atualizar Swagger, manual QA e changelog;
- manter entregas curtas com commit/push por task.
