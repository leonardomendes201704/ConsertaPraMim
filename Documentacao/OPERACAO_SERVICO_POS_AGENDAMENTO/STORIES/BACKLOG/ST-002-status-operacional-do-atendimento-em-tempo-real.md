# ST-002 - Status operacional do atendimento em tempo real

Status: Backlog  
Epic: EPIC-001

## Objetivo

Permitir acompanhamento do andamento do servico com estados operacionais claros, atualizados em tempo real para cliente, prestador e admin.

## Criterios de aceite

- Pedido/agendamento suportam estados operacionais `A caminho`, `No local`, `Em atendimento`, `Aguardando peca`, `Concluido`.
- Regras de transicao impedem pulos invalidos e regressao indevida de estado.
- Cliente visualiza status atualizado sem refresh completo.
- Admin visualiza status operacional no detalhe do pedido e no dashboard.
- Historico de alteracao guarda ator, horario e motivo quando aplicavel.
- KPI de tempo em cada estado pode ser extraido para analytics.

## Tasks

- [ ] Definir enum de status operacional e matriz de transicoes validas.
- [ ] Adicionar campos de status operacional ao dominio de atendimento.
- [ ] Criar service para transicao com validacoes de papel (cliente/prestador/admin).
- [ ] Expor endpoint para alterar status operacional com motivo opcional.
- [ ] Publicar evento SignalR para refresh parcial do card de atendimento.
- [ ] Ajustar UI prestador com seletor rapido de status na agenda/detalhes.
- [ ] Ajustar UI cliente para mostrar etapa atual com timeline.
- [ ] Ajustar dashboard admin com filtros por estado operacional.
- [ ] Criar testes unitarios da maquina de estados.
- [ ] Criar testes E2E basicos de atualizacao em tempo real.
