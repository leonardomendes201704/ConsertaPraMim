# ST-109 - Conclusao do servico e avaliacao bilateral

Status: Done
Epic: EPIC-JORNADA-001

## Objetivo

Fechar a jornada apos o atendimento com confirmacao de conclusao e avaliacao do cliente sobre o prestador e do prestador sobre o cliente.

## Criterios de aceite

- O sistema consegue marcar servico como concluido.
- Cliente e prestador recebem solicitacao de avaliacao.
- As duas avaliacoes ficam vinculadas ao mesmo caso.
- O Kanban fecha a jornada automaticamente.

## Tasks

- [x] Definir evento de conclusao e suas origens validas.
- [x] Criar fluxo de cobranca de avaliacao para cliente.
- [x] Criar fluxo de cobranca de avaliacao para prestador.
- [x] Persistir avaliacoes, comentarios e motivos de nota baixa.
- [x] Atualizar score operacional do prestador e historico do cliente.
- [x] Cobrir no-show, cancelamento tardio e servico contestado.

## Entrega implementada

- O `JourneyProviderConnectionService` passou a iniciar automaticamente a etapa de encerramento da jornada assim que o aceite vencedor conecta cliente e prestador.
- O `JourneyServiceClosureLinkService` gera links assinados, expiraveis e auditaveis para:
  - desfecho do atendimento pelo prestador;
  - confirmacao ou contestacao da conclusao pelo cliente;
  - avaliacao bilateral pos-servico.
- O `JourneyServiceClosureController` expõe as paginas publicas do encerramento e das avaliacoes, sempre em PT-BR e com resposta oficial registrada apenas no `POST`.
- A jornada passou a persistir `ClosureStatus`, `ClosureSummary`, `ClosureOutcome`, timestamps de servico/conclusao/contestacao e os snapshots da avaliacao do cliente e do prestador.
- O modal do lead no Kanban ganhou a secao `Encerramento e avaliacoes`, exibindo status, desfecho, contestacao, etapas pendentes e o conteudo das avaliacoes quando enviadas.
- `Cliente nao compareceu`, `Cancelamento tardio` e `Conclusao contestada` agora levam a jornada para `Excecao operacional`, com historico auditavel em PT-BR.
- A avaliacao do cliente move o card para `Aguardando avaliacao do prestador`; a avaliacao final do prestador conclui automaticamente a jornada.
