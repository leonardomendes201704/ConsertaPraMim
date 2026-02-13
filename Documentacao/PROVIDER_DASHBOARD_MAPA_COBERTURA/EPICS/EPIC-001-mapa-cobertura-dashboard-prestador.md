# EPIC-001 - Mapa de cobertura e oportunidades geograficas do prestador

## Objetivo

Permitir que o prestador visualize na dashboard o proprio ponto base, o raio de interesse configurado e os pedidos proximos no mapa, incluindo pedidos dentro e fora do raio para ampliar contexto de mercado local.

## Problema atual

- A dashboard mostra contadores e lista, mas nao mostra contexto geografico.
- O prestador nao visualiza claramente o alcance do raio configurado.
- Nao existe visao espacial para entender oportunidades um pouco fora do raio atual.

## Resultado esperado

- Mapa exibido na dashboard com marcador do prestador e circulo de cobertura.
- Pins de pedidos proximos exibidos mesmo quando fora do raio de interesse.
- Diferenciacao visual entre pin dentro e fora do raio.
- Atualizacao de dados do mapa sem refresh completo da pagina.

## Metricas de sucesso

- 100% dos prestadores com base geocodificada veem mapa e raio corretamente.
- Reducao de atrito para ajuste de raio e categorias pelo prestador.
- Tempo de renderizacao do mapa abaixo de 2 segundos em ambiente local apos carregamento da dashboard.

## Escopo

### Inclui

- Backend para fornecer dados geograficos de pedidos (lat/lng, distancia, status dentro/fora do raio).
- UI da dashboard do prestador com mapa e legenda visual.
- Atualizacao dos pins em eventos de atualizacao da dashboard.

### Nao inclui

- Roteirizacao ponto-a-ponto.
- Calculo de trajeto por transito em tempo real.
- Clusterizacao avancada de pins com agregacoes por cidade.

## Historias vinculadas

- ST-001 - Dashboard com mapa, raio e pins dentro/fora do raio.
- ST-002 - Filtros e experiencia de navegacao geoespacial.
- ST-003 - Atualizacao em tempo real e otimizações de performance do mapa.
