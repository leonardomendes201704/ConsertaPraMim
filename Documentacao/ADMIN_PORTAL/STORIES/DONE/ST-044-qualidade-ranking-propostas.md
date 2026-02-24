# ST-044 - Qualidade e ranking de propostas por completude e historico

Status: Done
Epic: EPIC-018

## Objetivo

Melhorar qualidade media das propostas com scoring de completude, clareza e historico de entrega do prestador.

## Criterios de aceite

- Proposta recebe score de qualidade no envio.
- Regras minimas obrigatorias para envio (escopo, prazo, garantia).
- Ranking considera qualidade + historico operacional.
- Admin acompanha distribuicao de qualidade por categoria.

## Rubric de qualidade (v1)

- `Completude (40%)`: proposta informa escopo (`message`), prazo estimado (`estimatedLeadTimeHours`) e garantia (`warrantyDays`).
- `Clareza tecnica (25%)`: texto da proposta com detalhamento operacional minimo (>= 40 chars) e sem corpo vazio.
- `Historico do prestador (25%)`: reputacao e volume operacional (rating, reviewCount, servicos concluidos).
- `Confiabilidade comercial (10%)`: valor estimado informado e dentro de faixa esperada para a categoria.

### Formula inicial

- `proposalQualityScore = (completude * 0.40) + (clareza * 0.25) + (historico * 0.25) + (comercial * 0.10)`
- Escala final de 0 a 100, com arredondamento para 2 casas.

### Faixas operacionais

- `>= 85`: Excelente (destacar no ranking para cliente).
- `70-84.99`: Boa (elegivel para topo dependendo de preco/prazo).
- `50-69.99`: Regular (exibir recomendacao de melhoria ao prestador).
- `< 50`: Baixa (depriorizar no ranking e acionar alerta de qualidade no admin).

## Tasks

- [x] Definir rubric de qualidade de proposta.
- [x] Implementar validacoes obrigatorias no backend.
- [x] Implementar score e ranking por proposta.
- [x] Expor score em tela de proposta para cliente.
- [x] Adicionar painel admin de qualidade media por categoria.
