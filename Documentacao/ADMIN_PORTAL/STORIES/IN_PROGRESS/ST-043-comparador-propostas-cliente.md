# ST-043 - Comparador de propostas para decisao do cliente

Status: In Progress
Epic: EPIC-018

## Objetivo

Aumentar conversao proposta -> aceite com comparacao estruturada de propostas (preco, prazo, historico, garantia).

## Criterios de aceite

- Cliente consegue comparar propostas lado a lado.
- Ordenacao por criterios (menor preco, menor prazo, melhor score).
- Evidencia clara de diferencas e condicoes.
- Telemetria de uso do comparador e impacto na conversao.

## Tasks

- [x] Definir modelo comparativo padrao de propostas.
- [ ] Ajustar payload de proposta para campos de comparacao.
- [ ] Implementar UI de comparador no app/portal cliente.
- [ ] Instrumentar evento de interacao e aceite apos comparacao.
- [ ] Validar impacto em A/B test controlado.

## Modelo comparativo adotado

- `best_score`: score composto por preco, prazo de inicio, garantia e historico do prestador.
- `lowest_price`: prioriza menor valor estimado.
- `fastest_lead_time`: prioriza menor prazo de inicio em horas.
- `best_rating`: prioriza nota media e volume de avaliacoes do prestador.
- `highest_warranty`: prioriza maior garantia em dias.
