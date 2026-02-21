# Padrao de nomes de testes no VS2026

## Objetivo

Padronizar como os testes aparecem no Test Explorer para leitura rapida, triagem de falhas e manutencao.

## Padrao oficial de DisplayName

Formato:

`<Modulo> | <Acao/Cenario> | Deve <resultado> quando <condicao>`

Exemplo:

`Agendamentos controller | Cancelar agendamento | Deve retornar conflito quando nao ha slots`

## Regras obrigatorias

- Todo `[Fact]` e `[Theory]` deve conter `DisplayName`.
- O texto deve ser objetivo e em portugues (sem abreviacoes internas de codigo).
- Evitar nomes tecnicos de implementacao no DisplayName.
- Priorizar linguagem de comportamento/resultado.

## Traits recomendadas

- `Area`: dominio funcional (ex.: Agendamentos, Suporte, Pagamentos).
- `Camada`: Controller, Service, Repository, Integration, E2E.
- `Tipo`: Unitario, Integracao, E2E, Performance.

## Ferramentas da padronizacao

- Runner config: `Backend/tests/ConsertaPraMim.Tests.Unit/xunit.runner.json`
- Script de apoio:
  - `Backend/tests/ConsertaPraMim.Tests.Unit/tools/Standardize-TestDisplayNames.ps1 -Apply`
  - `Backend/tests/ConsertaPraMim.Tests.Unit/tools/Standardize-TestDisplayNames.ps1 -Check`

## Politica para novos testes

- Novo teste sem `DisplayName` nao deve entrar em branch principal.
- Refatoracao de nomes deve manter semantica do cenario original.
