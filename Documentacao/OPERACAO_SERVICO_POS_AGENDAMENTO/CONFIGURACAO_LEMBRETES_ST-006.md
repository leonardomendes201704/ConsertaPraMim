# ST-006 - Configuracao de horarios de lembrete e confirmacao de presenca

Este guia centraliza a configuracao do motor de lembretes do agendamento (`ServiceAppointmentReminderWorker` + `AppointmentReminderService`).

## Onde configurar

Arquivo base:

- `Backend/src/ConsertaPraMim.API/appsettings.json`

Sobrescrita local:

- `Backend/src/ConsertaPraMim.API/appsettings.Development.json`

Secao:

- `ServiceAppointments:Reminders`

## Chaves suportadas

### `EnableWorker`

- Tipo: `bool`
- Default: `true`
- Uso: habilita/desabilita o worker de processamento.

### `WorkerIntervalSeconds`

- Tipo: `int`
- Default: `30`
- Limites aplicados em runtime: `5..3600`
- Uso: intervalo entre ciclos do worker.

### `BatchSize`

- Tipo: `int`
- Default: `200`
- Limites aplicados em runtime: `1..2000`
- Uso: quantidade maxima de dispatches processados por ciclo.

### `MaxAttempts`

- Tipo: `int`
- Default: `3`
- Limites aplicados em runtime: `1..10`
- Uso: tentativas maximas por lembrete antes de `FailedPermanent`.

### `RetryBaseDelaySeconds`

- Tipo: `int`
- Default: `60` (`30` em Development)
- Limites aplicados em runtime: `5..3600`
- Uso: base do backoff exponencial (`base * 2^(tentativa-1)`), limitado a `21600s`.

### `OffsetsMinutes`

- Tipo: `array<int>`
- Default: `[1440, 120, 30]`
- Limites aplicados em runtime por item: `1..10080`
- Uso: janelas (em minutos antes da visita) para lembrete padrao.
- Observacao: valores sao normalizados (distinct + ordenacao decrescente).

### `PresenceConfirmationOffsetsMinutes`

- Tipo: `array<int>`
- Default: `[120]` (quando nao informado)
- Uso: subset de `OffsetsMinutes` que gera lembrete com CTA de confirmacao de presenca.
- Regra: qualquer valor fora de `OffsetsMinutes` e descartado.

## Regra de calculo do disparo

Para cada participante elegivel e canal habilitado:

1. `ScheduledForUtc = WindowStartUtc - offset`.
2. `NextAttemptAtUtc = max(ScheduledForUtc, UtcNow)`.
3. Se canal/fila falhar, aplica backoff e marca `FailedRetryable` ate atingir `MaxAttempts`.

## Exemplo recomendado (producao)

```json
"ServiceAppointments": {
  "Reminders": {
    "EnableWorker": true,
    "WorkerIntervalSeconds": 30,
    "BatchSize": 200,
    "MaxAttempts": 3,
    "RetryBaseDelaySeconds": 60,
    "OffsetsMinutes": [1440, 120, 30],
    "PresenceConfirmationOffsetsMinutes": [120]
  }
}
```

## Exemplo para ambiente de desenvolvimento (mais rapido)

```json
"ServiceAppointments": {
  "Reminders": {
    "EnableWorker": true,
    "WorkerIntervalSeconds": 15,
    "BatchSize": 200,
    "MaxAttempts": 3,
    "RetryBaseDelaySeconds": 30,
    "OffsetsMinutes": [1440, 120, 30],
    "PresenceConfirmationOffsetsMinutes": [120]
  }
}
```

## Checklist de validacao rapida

1. Confirmar agendamento (`Confirmed` ou `RescheduleConfirmed`).
2. Verificar criacao de registros em `AppointmentReminderDispatches`.
3. Validar que existe pelo menos um evento com `:presence` no `EventKey`.
4. Aguardar ciclo do worker e conferir transicao para `Sent` ou `FailedRetryable`.
5. Responder presenca na UI e confirmar preenchimento de:
   - `ResponseReceivedAtUtc`
   - `ResponseConfirmed`
   - `ResponseReason` (quando informado)

## Observacoes operacionais

- Os horarios do agendamento e dos calculos internos de lembrete estao em UTC.
- Preferencias por usuario/canal podem silenciar canal e sobrescrever offsets via `PreferredOffsetsMinutesCsv`.
- Duplicidade e evitada por `EventKey` + indice unico em banco.
