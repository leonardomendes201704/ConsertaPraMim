# ST-027 - Refatoracao completa dos nomes de testes no VS2026

Status: Done  
Epic: EPIC-009

## Objetivo

Como time de desenvolvimento, quero visualizar testes no VS2026 com nomes claros, em portugues e orientados a comportamento para acelerar depuracao e manutencao.

## Criterios de aceite

- Todos os testes `[Fact]` e `[Theory]` possuem `DisplayName`.
- Os nomes exibidos seguem padrao unico e objetivo em PT-BR.
- Existe configuracao de runner para melhor leitura no Test Explorer.
- Existe guia de padrao para novos testes.
- Build e testes passam sem regressao funcional.

## Tasks

- [x] Definir plano de padronizacao e registrar EPIC/STORY.
- [x] Criar infraestrutura de padrao (runner config + script de apoio + guia).
- [x] Refatorar testes de `Controllers`, `Middleware` e `Validators`.
- [x] Refatorar testes de `Services`.
- [x] Refatorar testes de `Integration` e `E2E`.
- [x] Executar validacao final (`build` + `test`) e ajustar inconsistencias.
- [x] Publicar resumo final com metricas da migracao.

## Resultado da validacao

- `dotnet build src.sln`: sucesso.
- `dotnet test ConsertaPraMim.Tests.Unit`: sem regressao de compilacao; suite completa apresentou falhas preexistentes em cenarios SQLite (`near \"max\": syntax error`) nao relacionados a renomeacao dos testes.

## Metricas da migracao

- Total de testes detectados: 413.
- Testes com `DisplayName` antes: 0.
- Testes com `DisplayName` depois: 413.
- Cobertura da padronizacao de nome visivel: 100%.
