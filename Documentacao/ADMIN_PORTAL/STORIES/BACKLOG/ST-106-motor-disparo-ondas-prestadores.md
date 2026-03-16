# ST-106 - Motor de disparo em ondas para prestadores

Status: Backlog
Epic: EPIC-JORNADA-001

## Objetivo

Disparar oportunidades em ondas controladas para maximizar aceite sem gerar spam para a base de prestadores.

## Criterios de aceite

- O sistema envia oportunidades em ondas configuraveis.
- O disparo para automaticamente quando houver aceite valido.
- O sistema registra expiracao, recusa e ausencia de resposta.

## Tasks

- [ ] Criar entidades `ServiceDispatchWave` e `ServiceDispatchTarget`.
- [ ] Definir tamanho e timeout de cada onda.
- [ ] Parar ondas futuras quando o caso for reservado.
- [ ] Criar fila de disparo com idempotencia por jornada, prestador e onda.
- [ ] Medir aceite por onda e por categoria.
- [ ] Cobrir corrida entre dois aceites quase simultaneos.
