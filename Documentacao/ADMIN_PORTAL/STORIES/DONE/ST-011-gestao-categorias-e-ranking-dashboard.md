# ST-011 - Gestao de categorias e ranking de pedidos por categoria no dashboard

Status: Done  
Epic: EPIC-002

## Objetivo

Como administrador, quero cadastrar e alterar categorias de servicos e visualizar no dashboard a quantidade de pedidos por categoria, ordenada da maior para a menor.

## Criterios de aceite

- Admin consegue criar nova categoria com validacao de nome unico.
- Admin consegue alterar categoria existente sem perder historico de pedidos.
- Admin consegue ativar/inativar categoria (sem exclusao fisica quando houver pedidos vinculados).
- Ao inativar categoria, pedidos ja existentes (inclusive nao finalizados) continuam funcionais em todo o fluxo.
- Categoria inativa nao pode ser usada na criacao de novos pedidos.
- Tela de pedidos/fluxos de criacao passam a usar catalogo de categorias ativo.
- Dashboard admin exibe lista/ranking com `Categoria` e `Quantidade de pedidos`.
- Ranking e ordenado do maior volume para o menor.
- Em empate de quantidade, ordenar por nome da categoria em ordem alfabetica.

## Tasks

- [x] Definir modelo persistente de `ServiceCategory` administravel (Id, Nome, Slug, Ativo, datas).
- [x] Criar migration e seed inicial com categorias atuais em PT-BR.
- [x] Implementar estrategia de compatibilidade/backfill para pedidos ja existentes.
- [x] Criar API Admin para listar, criar, editar e ativar/inativar categorias.
- [x] Adicionar validacoes de dominio: nome obrigatorio, unico, tamanho maximo e slug unico.
- [x] Proteger categoria em uso contra exclusao fisica; usar inativacao.
- [x] Garantir regra de transicao: inativacao nao afeta execucao de pedidos ja existentes/nao finalizados.
- [x] Bloquear criacao de novos pedidos quando a categoria estiver inativa.
- [x] Atualizar fluxo de criacao de pedido para consumir categorias ativas do catalogo.
- [x] Atualizar telas que exibem categoria para usar nome do catalogo.
- [x] Expor no endpoint do dashboard admin o agregado `pedidos por categoria`.
- [x] Ordenar agregado por quantidade desc e nome asc (criterio de desempate).
- [x] Implementar card/tabela no dashboard admin para ranking por categoria.
- [x] Criar testes unitarios para CRUD de categorias e agregacao do dashboard.
- [x] Criar testes de regressao para bloqueio de novos pedidos com categoria inativa.
- [x] Criar testes de integracao para CRUD de categorias e agregacao do dashboard.
- [x] Criar testes de regressao para continuidade de pedidos abertos apos inativacao da categoria.
- [x] Atualizar INDEX/changelog do board administrativo.

## Progresso desta iteracao

- Catalogo de categorias persistente implementado em `ServiceCategoryDefinitions`, com relacao opcional em `ServiceRequests`.
- Fluxo de criacao de pedido migrado para categorias ativas do catalogo e bloqueio de categoria inativa.
- APIs admin/public de categorias implementadas (`api/admin/service-categories` e `api/service-categories/active`).
- Tela administrativa de gestao de categorias entregue no portal admin.
- Dashboard admin atualizado com ranking `pedidos por categoria` ordenado por volume.
- Cobertura unitaria adicionada para regras de categoria e ordenacao do dashboard.
- Cobertura de integracao adicionada para CRUD de categorias, agregacao do dashboard e regressao de continuidade de pedidos abertos apos inativacao.
