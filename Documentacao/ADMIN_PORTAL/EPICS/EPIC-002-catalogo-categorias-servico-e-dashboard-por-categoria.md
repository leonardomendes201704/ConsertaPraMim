# EPIC-002 - Catalogo de categorias de servico e dashboard por categoria

Status: In Progress

## Objetivo

Permitir que o administrador gerencie as categorias de servicos (cadastro e alteracao) e acompanhe no dashboard o volume de pedidos por categoria, ordenado da maior demanda para a menor.

## Problema atual

- Categorias de servico estao acopladas ao codigo e exigem deploy para ajustes.
- Time administrativo nao consegue governar rapidamente o catalogo de categorias.
- Dashboard nao mostra ranking de demanda por categoria.

## Resultado esperado

- CRUD administrativo para categorias de servico (com governanca de status ativo/inativo).
- Fluxos de pedidos consumindo categorias gerenciadas no catalogo.
- Inativacao de categoria bloqueia apenas novos pedidos, sem quebrar pedidos ja existentes.
- Dashboard admin exibindo quantidade de pedidos por categoria.
- Ranking exibido em ordem decrescente (mais pedidos para menos pedidos).

## Metricas de sucesso

- 100% das novas categorias podem ser criadas/alteradas sem mudanca de codigo.
- 100% dos pedidos novos vinculados a uma categoria valida.
- Dashboard administrativo apresenta ranking por categoria em menos de 2 segundos na carga inicial.
- Reducao de chamados tecnicos para alteracao de categoria.

## Escopo

### Inclui

- Modelagem persistente para categorias de servico administraveis.
- Endpoints administrativos para criar, editar, listar e ativar/inativar categorias.
- Integracao do catalogo nos fluxos de criacao/exibicao de pedidos.
- KPI no dashboard admin com quantidade de pedidos por categoria, ordenado.

### Nao inclui

- Motor de recomendacao automatica de categorias por IA.
- Estrutura hierarquica multinivel (categoria/subcategoria) nesta fase.
- Relatorios historicos exportaveis avancados (BI externo).

## Historias vinculadas

- ST-011 - Gestao de categorias e ranking de pedidos por categoria no dashboard.
