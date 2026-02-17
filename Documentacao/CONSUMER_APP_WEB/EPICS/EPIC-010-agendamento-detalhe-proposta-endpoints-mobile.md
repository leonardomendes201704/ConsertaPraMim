# EPIC-010 - Agendamento no detalhe da proposta com endpoints mobile dedicados

Status: Done
Trilha: CONSUMER_APP_WEB

## Objetivo

Levar para o app o fluxo de agendamento no contexto da proposta (igual ao portal do cliente), mantendo contrato dedicado mobile para nao acoplar com endpoints dos portais.

## Problema de negocio

- O cliente via a proposta no app, mas nao conseguia seguir o proximo passo operacional (agendar visita) na mesma tela.
- O fluxo ficava quebrado entre aceite e execucao do servico.
- Havia risco de divergencia de contrato ao reutilizar endpoints de portal no app.

## Resultado esperado

- Tela de detalhes da proposta com secao de agendamento:
  - selecionar data;
  - informar observacao opcional;
  - buscar horarios disponiveis;
  - solicitar agendamento no slot escolhido.
- Endpoints dedicados mobile para slots e criacao de agendamento, sem quebrar portais.
- Reuso das mesmas regras de negocio da agenda ja existente no backend.
- Feedback claro no app para sucesso/erro/conflito.

## Historia vinculada

- ST-012 - Agendamento na tela de proposta com slots e solicitacao de visita
