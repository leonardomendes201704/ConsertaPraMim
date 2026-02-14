# ST-003 - Confirmacao do prestador, recusa e expiracao automatica

Status: Backlog  
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

- [ ] Implementar endpoint de confirmacao do prestador.
- [ ] Implementar endpoint de recusa do prestador com motivo obrigatorio.
- [ ] Configurar SLA de expiracao (valor inicial em configuracao).
- [ ] Criar job de expiracao para agendamentos pendentes vencidos.
- [ ] Publicar eventos/notificacoes para cliente e prestador.
- [ ] Garantir idempotencia de operacoes de confirmacao/recusa.
- [ ] Escrever testes para:
- [ ] confirmacao valida.
- [ ] recusa valida.
- [ ] tentativa de agir em agendamento ja finalizado.
- [ ] expiracao automatica por SLA.
