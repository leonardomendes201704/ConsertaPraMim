# ST-001 - Modelagem de agenda, disponibilidade e janelas de horario

Status: Done  
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
- [x] Validar impacto no seed atual e documentar ajustes necessarios.

## Validacao de impacto no seed atual (2026-02-16)

- O seed atual (`ConsertaPraMim.Infrastructure/Data/DbInitializer.cs`) nao cria dados iniciais de:
  - `ProviderAvailabilityRule`
  - `ProviderAvailabilityException`
  - `ServiceAppointment`
- A consulta de slots (`ServiceAppointmentService.GetAvailableSlotsAsync`) retorna `success=true` com lista vazia quando o prestador nao possui regras de disponibilidade.
- Resultado pratico: apos `Seed:Reset=true`, o fluxo de agendamento permanece funcional, mas nenhum horario aparece ate que exista ao menos uma regra de disponibilidade ativa para o prestador.
- Timezone de disponibilidade: quando nao configurado em `ServiceAppointments:AvailabilityTimeZoneId`, o sistema usa fallback para `America/Sao_Paulo` (ou `E. South America Standard Time`).

## Ajustes necessarios

- Nao foi identificado ajuste obrigatorio no seed para manter compatibilidade com o modelo atual.
- Ajuste operacional recomendado para ambiente de QA/dev:
  - cadastrar regras base por prestador de teste (ex.: segunda a sexta, 08:00-18:00) via API/UI de disponibilidade;
  - opcionalmente evoluir o `DbInitializer` em historia futura para semear regras minimas quando `Seed:Reset=true`.
