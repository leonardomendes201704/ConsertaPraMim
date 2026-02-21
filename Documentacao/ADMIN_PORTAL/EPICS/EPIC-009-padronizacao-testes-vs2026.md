# EPIC-009 - Padronizacao de testes para VS2026 (nomes claros em PT-BR)

Status: In Progress
Trilha: ADMIN_PORTAL

## Objetivo

Padronizar a exibicao dos testes no Test Explorer (VS2026) com nomes claros, objetivos e em portugues, reduzindo ambiguidades e acelerando diagnostico de falhas.

## Problema de negocio

- Nomes atuais aparecem longos e tecnicos, com baixo valor para leitura rapida.
- Nao existe padrao obrigatorio para nome visivel de teste.
- Falhas em regressao exigem tempo maior para identificar cenario e impacto.

## Resultado esperado

- 100% dos testes exibidos no VS2026 com `DisplayName` padronizado em PT-BR.
- Convencao unica para novos testes (nome + traits + organizacao).
- Fluxo de validacao para evitar novos testes fora do padrao.

## Metricas de sucesso

- 100% dos `[Fact]` e `[Theory]` com `DisplayName`.
- 0 novos testes sem padrao apos ativacao da governanca.
- Reducao do tempo de leitura e triagem de falhas no Test Explorer.

## Escopo

### Inclui

- Convencao de nome visivel para testes.
- Refatoracao de todos os testes existentes para `DisplayName`.
- Padrao de organizacao no VS2026 (runner config e traits basicas).
- Script para apoiar padronizacao em lote e manutencao.

### Nao inclui

- Mudancas de regra de negocio dos testes.
- Reescrita de cenarios que ja estao corretos funcionalmente.

## Historias vinculadas

- ST-027 - Refatoracao completa dos nomes de testes no VS2026.
