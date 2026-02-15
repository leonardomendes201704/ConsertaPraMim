# ST-008 - Runbook operacional de no-show

Este runbook padroniza a atuacao do time admin para reduzir faltas, tratar risco de nao comparecimento e manter rastreabilidade completa das decisoes.

## 1. Objetivo

- Reduzir no-show em visitas agendadas.
- Padronizar resposta operacional para risco `medio` e `alto`.
- Garantir registro auditavel de cada acao.

## 2. Escopo

- Agendamentos com `NoShowRiskLevel = Medium` ou `High`.
- Fila operacional com itens `Open` e `InProgress`.
- Alertas de threshold enviados pelo worker de risco.

## 3. Fontes de verdade

- Painel: `GET /api/admin/no-show-dashboard`
- Exportacao: `GET /api/admin/no-show-dashboard/export`
- Configuracao de threshold: `GET/PUT /api/admin/no-show-alert-thresholds`
- Politica tecnica: `POLITICA_ACAO_RISCO_NO_SHOW_ST-007.md`

## 4. Criticidade e prioridade

Use esta ordem para atendimento:

1. Risco `High` com visita nas proximas 2h.
2. Risco `High` com visita nas proximas 6h.
3. Risco `Medium` com visita nas proximas 2h.
4. Demais itens `Medium`.

## 5. SLA operacional recomendado

- `High` (T-2h): iniciar acao em ate 10 minutos.
- `High` (T-6h): iniciar acao em ate 30 minutos.
- `Medium`: iniciar acao em ate 60 minutos.
- Revalidacao de itens sem resposta: a cada 30 minutos.

## 6. Fluxo de atuacao (passo a passo)

### Etapa A - Triagem

1. Abrir o painel admin e filtrar por periodo atual.
2. Ordenar a fila por `RiskLevel` desc e `Score` desc.
3. Selecionar o item de maior prioridade.
4. Mudar status para `InProgress` quando iniciar o tratamento.

### Etapa B - Contato preventivo

1. Contatar cliente e prestador (chat/notificacao) com pedido de confirmacao ativa.
2. Confirmar:
   - disponibilidade no horario
   - endereco e acesso ao local
   - canal de contato rapido
3. Se ambos confirmarem, registrar evidencias e seguir monitorando.

### Etapa C - Escalonamento

1. Se apenas um lado responder:
   - reforcar contato no outro lado em ate 15 minutos.
2. Se nao houver resposta apos 2 tentativas:
   - classificar como `Risco mantido sem confirmacao`.
   - manter na fila ate janela da visita.
3. Se houver impossibilidade declarada:
   - sugerir reagendamento imediato.
   - registrar motivo objetivo.

### Etapa D - Fechamento

Fechar item quando ocorrer um dos desfechos:

- `Confirmado`: cliente e prestador confirmados.
- `Reagendado`: nova janela aceita por ambos.
- `Cancelado`: visita cancelada antes da execucao.
- `NoShow`: nao comparecimento confirmado.
- `Nao conclusivo`: sem contato ate o horario.

Registrar nota final padrao com:

- quem foi contatado
- horario das tentativas
- resposta de cada parte
- acao tomada

## 7. Regras de registro (auditoria)

- Nunca fechar item sem nota final.
- Usar timestamps UTC no registro tecnico.
- Evitar texto livre generico ("ok", "feito"); detalhar acao.
- Toda decisao de cancelamento/reagendamento deve indicar origem (cliente, prestador, operacao).

## 8. Playbooks rapidos

### PB-01 - Cliente confirma, prestador sem resposta

1. Segunda tentativa com prestador em 15 minutos.
2. Se silencio persistir e janela estiver proxima (<2h), elevar prioridade.
3. Orientar cliente sobre possivel reagendamento preventivo.

### PB-02 - Prestador confirma, cliente sem resposta

1. Segunda tentativa com cliente em 15 minutos.
2. Validar endereco e disponibilidade de acesso.
3. Se silencio persistir, sinalizar risco de no-show do cliente.

### PB-03 - Ambos sem resposta

1. Registrar duas tentativas por canal.
2. Manter item em monitoramento ate a janela.
3. Pos janela, classificar desfecho conforme evento real.

## 9. Governanca por KPI (acao gatilho)

Quando alertas automaticos forem disparados:

1. Validar filtros e periodo que originaram o alerta.
2. Comparar taxa atual com baseline historico (ultimos 30 dias).
3. Abrir acao corretiva operacional se threshold `critical` for atingido.
4. Revisar pesos/thresholds da heuristica se houver falso positivo recorrente.

## 10. Exportacao para analise externa

Use o endpoint CSV para analise offline:

- Endpoint: `GET /api/admin/no-show-dashboard/export`
- Filtros suportados: `fromUtc`, `toUtc`, `city`, `category`, `riskLevel`, `queueTake`, `cancellationNoShowWindowHours`
- Conteudo exportado:
  - linha consolidada de KPI
  - breakdown por categoria
  - breakdown por cidade
  - itens abertos de fila de risco

## 11. Checklist diario da operacao

- [ ] Revisar fila de risco no inicio do turno.
- [ ] Tratar itens `High` com SLA.
- [ ] Verificar alertas de threshold warning/critical.
- [ ] Registrar desfechos de todos os itens tratados.
- [ ] Exportar CSV diario para analise e historico operacional.

