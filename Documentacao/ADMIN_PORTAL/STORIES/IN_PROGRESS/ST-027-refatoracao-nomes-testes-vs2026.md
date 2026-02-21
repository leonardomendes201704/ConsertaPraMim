# ST-027 - Refatoracao completa dos nomes de testes no VS2026

Status: In Progress  
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
- [ ] Criar infraestrutura de padrao (runner config + script de apoio + guia).
- [ ] Refatorar testes de `Controllers`, `Middleware` e `Validators`.
- [ ] Refatorar testes de `Services`.
- [ ] Refatorar testes de `Integration` e `E2E`.
- [ ] Executar validacao final (`build` + `test`) e ajustar inconsistencias.
- [ ] Publicar resumo final com metricas da migracao.
