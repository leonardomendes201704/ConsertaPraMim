# EPIC-006 - Creditos de premio para abatimento da mensalidade do prestador

Status: In Progress

## Objetivo

Permitir que o admin conceda creditos para prestadores, com comunicacao de "premio", e que esses creditos sejam usados automaticamente para abater a mensalidade dos planos.

## Problema atual

- Nao existe mecanismo de saldo de credito para prestador.
- O admin nao consegue conceder premio financeiro operacional sem alterar preco/plano.
- Nao ha trilha de extrato de credito para auditoria.
- O calculo de mensalidade nao considera saldo concedido manualmente.

## Resultado esperado

- Admin consegue conceder e estornar creditos de forma rastreavel.
- Prestador recebe alerta em tempo real quando recebe premio/credito.
- Sistema aplica credito na mensalidade automaticamente, respeitando vigencia e saldo.
- Prestador e admin conseguem visualizar extrato, saldo e consumo de credito.

## Metricas de sucesso

- 100% das concessoes de premio realizadas sem operacao manual no banco.
- Tempo medio para conceder premio < 2 minutos.
- 0 divergencias entre saldo exibido e extrato transacional.
- 100% dos lancamentos com auditoria (quem, quando, motivo, valor, origem).

## Escopo

### Inclui

- Carteira de creditos por prestador com ledger transacional imutavel.
- Concessao de credito pelo admin (premio/campanha/ajuste) com validade.
- Notificacao ao prestador sobre premio recebido.
- Motor de aplicacao de credito no calculo da mensalidade.
- Visibilidade de saldo/extrato no portal do prestador e no portal admin.
- Regras de governanca e auditoria para concessao/estorno/consumo.

### Nao inclui

- Integracao com gateway de pagamento real.
- Regras fiscais/tributarias de cashback.
- Transferencia de credito entre prestadores.

## Historias vinculadas

- ST-015 - Carteira de creditos e ledger transacional do prestador.
- ST-016 - Concessao administrativa de creditos e notificacao de premio.
- ST-017 - Aplicacao de creditos na mensalidade e visibilidade operacional.

## Progresso atual

- ST-015 concluida (modelo de carteira/ledger, servico, endpoints de consulta, auditoria e testes iniciais).
- ST-016 e ST-017 permanecem em backlog.
