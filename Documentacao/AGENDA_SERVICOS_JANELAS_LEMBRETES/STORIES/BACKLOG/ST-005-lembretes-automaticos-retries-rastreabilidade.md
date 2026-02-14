# ST-005 - Lembretes automaticos, retries e rastreabilidade de envio

Status: Backlog  
Epic: EPIC-001

## Objetivo

Criar o fluxo automatizado de lembretes de agendamento com confiabilidade operacional e rastreabilidade de entregas.

## Criterios de aceite

- Lembretes sao programados para `T-24h`, `T-2h` e `T-30min` apos agendamento confirmado.
- Cancelamento/reagendamento atualiza ou invalida lembretes antigos.
- Falhas de envio entram em retry com limite de tentativas.
- Historico de envio fica consultavel para suporte/admin.
- Duplicidade de lembrete para o mesmo evento e canal e evitada.

## Tasks

- [ ] Modelar entidade de fila/log de lembretes (`AppointmentReminderDispatch`).
- [ ] Implementar scheduler para criar lembretes no momento da confirmacao.
- [ ] Implementar worker para processar lembretes pendentes.
- [ ] Integrar canais iniciais: notificacao in-app e email.
- [ ] Implementar politica de retry com backoff.
- [ ] Implementar idempotencia por chave de evento/canal.
- [ ] Registrar status final de envio: sucesso, falha permanente, cancelado.
- [ ] Expor endpoint/admin query para rastreabilidade de lembretes.
- [ ] Escrever testes de resiliencia e idempotencia.
