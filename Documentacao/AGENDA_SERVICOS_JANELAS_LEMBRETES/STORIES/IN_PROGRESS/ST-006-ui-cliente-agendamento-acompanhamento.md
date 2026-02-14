# ST-006 - UI do cliente para agendar, acompanhar e reagendar servicos

Status: In Progress  
Epic: EPIC-001

## Objetivo

Entregar experiencia completa para o cliente criar agendamento, acompanhar status e acionar reagendamento/cancelamento.

## Criterios de aceite

- Cliente visualiza slots disponiveis e agenda em poucos cliques.
- Cliente visualiza lista de agendamentos futuros e passados.
- Cliente recebe feedback visual de status: pendente, confirmado, reagendamento, cancelado.
- Cliente consegue solicitar reagendamento e cancelamento na UI.
- Atualizacoes de status aparecem sem refresh completo (tempo real/polling controlado).

## Tasks

- [x] Mapear jornadas de tela do cliente:
- [x] criar agendamento.
- [x] acompanhar agendamentos.
- [x] solicitar reagendamento/cancelamento.
- [x] Implementar componentes de calendario/lista de slots.
- [x] Integrar UI com endpoints de agenda.
- [x] Exibir timeline de status e historico resumido.
- [x] Adicionar validacoes de formulario e mensagens de erro claras.
- [x] Integrar notificacoes visuais (toast/badge) para mudancas de estado.
- [ ] Cobrir fluxo com testes de interface e regressao funcional.
