# EPIC-003 - Aditivos de escopo e valor durante atendimento

## Objetivo

Permitir ajuste controlado de escopo e preco durante a execucao, com aprovacao explicita do cliente e trilha de auditoria comercial.

## Problema atual

- Mudancas tecnicas comuns em campo nao possuem fluxo formal.
- Ajustes combinados no chat geram ambiguidades e disputas.
- Nao ha versionamento da proposta apos aceite inicial.

## Resultado esperado

- Prestador solicita aditivo com justificativa e evidencias.
- Cliente aprova ou rejeita diretamente no fluxo do pedido.
- Sistema registra versoes de valor/escopo antes e depois.
- Operacao/admin consegue auditar decisao e impacto financeiro.

## Escopo

### Inclui

- Modelo de aditivo com motivo, valor incremental e prazo.
- Workflow de aprovacao/rejeicao.
- Atualizacao do valor total do pedido com historico.

### Nao inclui

- Negociacao multi-rodada ilimitada com contraproposta complexa.
- Motor de precificacao automatica por IA.

## Metricas de sucesso

- >= 95% dos ajustes com aceite explicito registrado.
- Reducao de reclamacoes por valor divergente em >= 40%.
- Tempo medio de decisao do cliente < 20 min durante atendimento.

## Historias vinculadas

- ST-009 - Solicitacao de aditivo de escopo e valor pelo prestador.
- ST-010 - Aprovacao do cliente e versionamento comercial do pedido.
