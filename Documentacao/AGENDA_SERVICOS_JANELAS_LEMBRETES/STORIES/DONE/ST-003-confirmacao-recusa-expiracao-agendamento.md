# ST-003 - Confirmacao do prestador, recusa e expiracao automatica

Status: Done  
Epic: EPIC-001

## Objetivo

Implementar o fluxo de decisao do prestador sobre uma solicitacao de agendamento, com expiracao automatica quando nao houver resposta no SLA.

## Criterios de aceite

- Prestador consegue confirmar ou recusar agendamento pendente.
- Recusa exige motivo registrado.
- Agendamento pendente expira automaticamente apos SLA sem resposta.
- Cliente recebe notificacao para confirmacao, recusa ou expiracao.
- Historico registra ator, acao e timestamp em todas as transicoes.

## Tasks

- [x] Implementar endpoint de confirmacao do prestador.
- [x] Implementar endpoint de recusa do prestador com motivo obrigatorio.
- [x] Configurar SLA de expiracao (valor inicial em configuracao).
- [x] Criar job de expiracao para agendamentos pendentes vencidos.
- [x] Publicar eventos/notificacoes para cliente e prestador.
- [x] Garantir idempotencia de operacoes de confirmacao/recusa.
- [x] Escrever testes para:
- [x] confirmacao valida.
- [x] recusa valida.
- [x] tentativa de agir em agendamento ja finalizado.
- [x] expiracao automatica por SLA.
