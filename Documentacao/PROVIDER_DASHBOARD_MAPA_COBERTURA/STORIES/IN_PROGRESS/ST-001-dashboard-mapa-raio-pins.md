# ST-001 - Dashboard com mapa, raio e pins dentro/fora do raio

Status: In Progress  
Epic: EPIC-001

## Objetivo

Adicionar na dashboard do prestador um mapa com visualizacao do raio de interesse e pins de pedidos proximos, incluindo pedidos fora do raio.

## Criterios de aceite

- Dashboard do prestador exibe mapa quando houver base geocodificada.
- Mapa mostra marcador do prestador e circulo do raio de interesse.
- Pins exibem pedidos dentro e fora do raio com diferenciacao visual.
- Popup do pin exibe informacoes basicas e atalho para detalhes do pedido.
- Ausencia de base geocodificada exibe estado vazio com orientacao para configurar perfil.

## Tasks

- [ ] Definir DTO de pin de mapa para pedidos do prestador.
- [ ] Implementar servico para recuperar pedidos proximos para mapa com distancia calculada.
- [ ] Expor dados de mapa no `HomeController` (load inicial e refresh).
- [ ] Implementar card/mapa na `Views/Home/Index.cshtml` com Leaflet.
- [ ] Diferenciar visualmente pin dentro do raio e fora do raio.
- [ ] Validar build completo do projeto e documentar observacoes.
