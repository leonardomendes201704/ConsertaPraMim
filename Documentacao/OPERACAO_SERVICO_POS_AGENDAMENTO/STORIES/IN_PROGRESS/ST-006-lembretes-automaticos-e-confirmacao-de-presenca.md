# ST-006 - Lembretes automaticos e confirmacao de presenca

Status: In Progress  
Epic: EPIC-002

## Objetivo

Enviar comunicacoes nos momentos chave da visita e coletar confirmacao de presenca de cliente e prestador para reduzir faltas.

## Criterios de aceite

- Lembretes sao disparados em janelas configuraveis (ex.: T-24h, T-2h, T-30min).
- Cliente e prestador podem responder `Confirmo` ou `Nao confirmo` pelo proprio aviso.
- Estado de confirmacao de presenca fica visivel no detalhe do agendamento.
- Falhas de envio geram retry com idempotencia.
- Nao ha notificacao duplicada para o mesmo evento/canal.
- Logs registram envio, entrega e resposta dos participantes.

## Tasks

- [x] Criar modelo de preferencias de lembrete por usuario/canal.
- [x] Evoluir worker de lembretes para etapa de confirmacao de presenca.
- [x] Criar endpoints para registrar resposta de presenca.
- [x] Adicionar campos de presenca no agendamento (cliente/prestador).
- [x] Publicar updates via SignalR apos resposta de presenca.
- [x] Implementar cards de acao rapida de confirmacao nas UIs.
- [ ] Implementar retries exponenciais e deduplicacao por chave de evento.
- [ ] Persistir telemetria de entrega e resposta por canal.
- [ ] Cobrir fluxos com testes de integracao do worker.
- [ ] Atualizar documentacao de configuracao de horarios de lembrete.
