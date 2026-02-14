# ST-002 - API de consulta de slots e criacao de agendamentos

Status: In Progress  
Epic: EPIC-001

## Objetivo

Disponibilizar endpoints para consulta de slots validos e criacao de agendamentos com protecao contra conflito de horario.

## Criterios de aceite

- Cliente consegue consultar slots disponiveis por prestador e intervalo de datas.
- API bloqueia criacao de agendamento fora da disponibilidade do prestador.
- API bloqueia overlap de agendamento confirmado para o mesmo prestador.
- Agendamento criado inicia em `PendingProviderConfirmation`.
- Endpoint de listagem de agendamentos por papel (cliente/prestador) disponivel.

## Tasks

- [x] Definir DTOs para consulta de slots e criacao de agendamento.
- [x] Implementar servico de calculo de slots livres considerando:
- [x] regras recorrentes de disponibilidade.
- [x] excecoes de agenda.
- [x] agendamentos existentes bloqueantes.
- [x] Expor endpoint `GET` de slots por prestador.
- [x] Expor endpoint `POST` para criar agendamento.
- [x] Expor endpoint `GET` para listar agendamentos do usuario logado.
- [x] Implementar validacoes de autorizacao por role.
- [x] Tratar concorrencia de criacao de agendamento no mesmo slot.
- [x] Criar testes unitarios de service/controller de slots/agendamento.
- [x] Criar testes de integracao de repositorio da agenda (sqlite/in-memory).
- [ ] Criar testes de integracao end-to-end da API de slots/agendamento (TestServer).
