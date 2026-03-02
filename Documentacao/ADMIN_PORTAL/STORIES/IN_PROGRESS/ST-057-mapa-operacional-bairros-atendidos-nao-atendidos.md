# ST-057 - Mapa Operacional com bairros atendidos e nao atendidos

Status: In Progress
Epic: EPIC-001

## Objetivo

Evoluir a tela `Mapa Operacional` do portal admin para evidenciar, de forma tabular e acionavel, quais bairros com pedidos estao cobertos por prestadores e quais bairros possuem gap de cobertura operacional.

## Criterios de aceite

- A tela `AdminCoverageMap/Index` continua exibindo o mapa operacional atual com pedidos e prestadores.
- Abaixo do mapa, o admin visualiza uma tabela de `Bairros atendidos`.
- Abaixo do mapa, o admin visualiza uma tabela de `Bairros nao atendidos`.
- A classificacao considera apenas bairros que tenham pelo menos um pedido com coordenadas validas.
- `Bairros atendidos` sao bairros cujos pedidos estao 100% dentro do raio de pelo menos um prestador.
- `Bairros nao atendidos` sao bairros com um ou mais pedidos sem cobertura por raio.
- Bairros sem nome devem aparecer como `Bairro nao informado`, sem quebrar a consolidacao.
- O comportamento respeita o filtro de cidade da tela e atualiza junto com o snapshot.
- Documentacao e QA do Portal Admin atualizados no mesmo ciclo.

## Tasks

- [x] Abrir story, epic, indice e changelog da entrega.
- [x] Estender o snapshot do coverage map para incluir bairro no payload.
- [ ] Renderizar tabelas de bairros atendidos e nao atendidos no portal admin.
- [ ] Adicionar teste de regressao, atualizar manual QA e encerrar a story.
