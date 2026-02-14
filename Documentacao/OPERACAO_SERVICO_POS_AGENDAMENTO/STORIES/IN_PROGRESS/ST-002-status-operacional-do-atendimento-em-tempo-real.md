# ST-002 - Status operacional do atendimento em tempo real

Status: In Progress  
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

- [x] Definir enum de status operacional e matriz de transicoes validas.
- [x] Adicionar campos de status operacional ao dominio de atendimento.
- [x] Criar service para transicao com validacoes de papel (cliente/prestador/admin).
- [x] Expor endpoint para alterar status operacional com motivo opcional.
- [x] Publicar evento SignalR para refresh parcial do card de atendimento.
- [x] Ajustar UI prestador com seletor rapido de status na agenda/detalhes.
- [x] Ajustar UI cliente para mostrar etapa atual com timeline.
- [x] Ajustar dashboard admin com filtros por estado operacional.
- [x] Criar testes unitarios da maquina de estados.
- [ ] Criar testes E2E basicos de atualizacao em tempo real.
