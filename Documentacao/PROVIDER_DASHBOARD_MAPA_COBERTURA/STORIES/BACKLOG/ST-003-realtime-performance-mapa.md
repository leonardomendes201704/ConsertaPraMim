# ST-003 - Atualizacao em tempo real e performance do mapa

Status: Backlog  
Epic: EPIC-001

## Objetivo

Evoluir a atualizacao dos pins para tempo real e otimizar performance de renderizacao conforme volume de pedidos.

## Criterios de aceite

- Novo pedido relevante atualiza mapa sem refresh completo.
- Refresh periodico/pontual nao degrada experiencia de uso.
- Dashboard mantem tempo de resposta adequado com alto volume local.

## Tasks

- [ ] Integrar atualizacao de mapa aos eventos existentes de notificacao.
- [ ] Implementar estrategia de refresh incremental dos pins.
- [ ] Definir limites de volume e paginacao geoespacial.
- [ ] Otimizar renderizacao frontend para muitos marcadores.
- [ ] Validar comportamento em cenarios de reconexao SignalR.
