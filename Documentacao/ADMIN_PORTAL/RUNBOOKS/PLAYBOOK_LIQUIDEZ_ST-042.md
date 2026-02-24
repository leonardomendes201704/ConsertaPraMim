# PLAYBOOK ST-042 - Score de Liquidez por Regiao/Categoria

Status: Ativo  
Story: ST-042  
Epic: EPIC-018

## Objetivo

Definir resposta operacional para cada faixa de score de liquidez no ConsertaPraMim, reduzindo pedidos sem proposta e acelerando a primeira resposta ao cliente.

## Formula aplicada

- `Score = cobertura_propostas (55%) + profundidade_oferta (20%) + velocidade_primeira_proposta (25%)`
- Cobertura de propostas: `% de pedidos com ao menos 1 proposta`
- Profundidade de oferta: relacao entre `prestadores distintos ativos na faixa` e demanda da faixa
- Velocidade: aderencia ao SLA de primeira proposta (minutos)

## Faixas de score

- `critical` (< 40 pontos): risco alto de perda de pedido por falta de liquidez.
- `warning` (>= 40 e < 65 pontos): liquidez insuficiente para meta de conversao.
- `healthy` (>= 65 pontos): operacao dentro da faixa recomendada.

## Acao por faixa

### 1) Critical

- Abrir plano de choque na regiao/categoria em ate 24h.
- Acionar captacao ativa de prestadores (campanha direta + base inativa).
- Priorizar push para prestadores online da categoria com incentivo tatico.
- Revisar SLA de triagem e distribuir carteira para prestadores com melhor tempo medio.
- Reavaliar em janela de 48h.

### 2) Warning

- Executar campanha de reforco comercial em ate 72h.
- Revisar qualidade das propostas (preco, prazo, completude).
- Rodar retencao de prestadores com baixa atividade na faixa.
- Monitorar tendencia diaria do score ate retorno para `healthy`.

### 3) Healthy

- Manter cadence semanal de acompanhamento.
- Preservar base ativa de prestadores com comunicacao recorrente.
- Validar se crescimento de demanda nao deteriora o SLA.

## Rotina operacional recomendada

1. Abrir `Score Liquidez` no portal admin.
2. Filtrar janela de 14 dias (padrao) e ordenar pelos menores scores.
3. Exportar top deficits para frente comercial/operacional.
4. Executar plano por faixa (`critical`/`warning`/`healthy`).
5. Reavaliar score e alertas em D+1 e D+7.

## Checklist rapido

- [ ] Top 10 grupos com `critical` revisados hoje.
- [ ] Plano de acao definido por grupo critico.
- [ ] Evolucao diaria registrada no acompanhamento.
- [ ] Alertas de liquidez sem regressao em 7 dias.
