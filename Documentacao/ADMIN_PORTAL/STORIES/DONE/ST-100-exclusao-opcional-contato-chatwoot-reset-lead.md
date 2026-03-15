# ST-100 - Exclusao opcional do contato no Chatwoot durante o reset do lead

Status: Done
Epic: EPIC-TELEGRAM-002

## Objetivo

Permitir que a operacao escolha no modal do CPM Full se a exclusao do lead deve apagar tambem o contato tecnico no Chatwoot, limpando o ambiente de teste sem depender de exclusao manual na plataforma externa.

## Entrega realizada

- O modal de detalhes do lead passou a exibir o checkbox `Excluir tambem o contato no Chatwoot`, desmarcado por padrao.
- O checkbox fica habilitado apenas quando o lead ja possui `ChatwootContactId`.
- Quando a opcao esta marcada, o CPM Full tenta excluir o contato remoto no Chatwoot antes da exclusao local.
- Se a delecao remota falhar, o reset local e bloqueado para evitar falsa percepcao de limpeza completa.
- Quando o contato remoto ja nao existe ou o lead ainda nao possui contato sincronizado, a mensagem final deixa esse estado explicito.

## Criterios de aceite atendidos

- O modal de exclusao do lead exibe checkbox opcional para limpar o contato remoto.
- O comportamento padrao continua seguro, sem exclusao automatica do Chatwoot.
- A exclusao local nao prossegue quando a API do Chatwoot falha na delecao remota solicitada.
- O fluxo deixa claro quando nao havia contato remoto sincronizado para apagar.

## Tasks

- [x] Estender o cliente da API do Chatwoot com delecao de contato.
- [x] Ajustar o modal do Kanban para exibir checkbox opcional com estado coerente.
- [x] Atualizar o controller para orquestrar delecao remota opcional + reset local.
- [x] Cobrir regressao e atualizar documentacao operacional.
