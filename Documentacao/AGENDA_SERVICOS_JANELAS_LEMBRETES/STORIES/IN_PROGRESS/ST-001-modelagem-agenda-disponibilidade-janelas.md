# ST-001 - Modelagem de agenda, disponibilidade e janelas de horario

Status: In Progress  
Epic: EPIC-001

## Objetivo

Criar a base de dominio e dados para suportar agenda de prestador, slots de horario, agendamentos e historico de mudancas.

## Criterios de aceite

- Entidades de agenda e agendamento criadas com relacionamento consistente ao pedido e ao prestador.
- Disponibilidade recorrente por dia da semana suportada.
- Excecoes de agenda (bloqueios pontuais) suportadas.
- Historico de transicoes de estado do agendamento persistido.
- Migration criada e aplicavel sem quebra do modelo atual.

## Tasks

- [x] Definir entidades de dominio:
- [x] `ProviderAvailabilityRule` (dia semana, hora inicio/fim, ativo).
- [x] `ProviderAvailabilityException` (data/hora inicio/fim, motivo).
- [x] `ServiceAppointment` (pedido, cliente, prestador, janela inicio/fim, estado).
- [x] `ServiceAppointmentHistory` (estado anterior, novo estado, ator, motivo, timestamp).
- [x] Definir enums de estado e acao de agenda.
- [x] Mapear entidades no `DbContext` com constraints e indices.
- [x] Criar migration inicial da agenda.
- [x] Criar repositories e contratos de acesso aos dados.
- [x] Cobrir regras basicas com testes de integracao de repositorio de agenda.
- [ ] Validar impacto no seed atual e documentar ajustes necessarios.
