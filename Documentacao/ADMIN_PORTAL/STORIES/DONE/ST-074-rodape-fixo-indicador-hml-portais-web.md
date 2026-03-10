# ST-074 - Rodape fixo de indicador HML nos portais web

## Como
Produto / operacao

## Eu quero
identificar visualmente quando um portal esta em homologacao

## Para
evitar uso acidental do ambiente HML como se fosse producao.

## Criterios de aceite

1. O rodape fixo deve aparecer somente quando o ambiente estiver em HML (`DEPLOY_PROFILE=development`).
2. O rodape deve estar presente nos quatro projetos web:
   - Portal Admin
   - Portal Cliente
   - Portal Prestador
   - Landing
3. Em producao (`DEPLOY_PROFILE=production`), o rodape nao deve ser renderizado.
4. O deploy VPS deve injetar `DEPLOY_PROFILE` tambem nos containers web, garantindo comportamento consistente entre PRD e HML.

## Tasks

- [x] adicionar deteccao de HML e renderizacao condicional do rodape no layout do portal admin;
- [x] adicionar deteccao de HML e renderizacao condicional do rodape no layout do portal cliente;
- [x] adicionar deteccao de HML e renderizacao condicional do rodape no layout do portal prestador;
- [x] adicionar deteccao de HML e renderizacao condicional do rodape no layout da landing;
- [x] atualizar compose de deploy (full + web services isolados) para expor `DEPLOY_PROFILE` nos projetos web;
- [x] atualizar runbook (`Backend/DEPLOY_VPS.md`) com a regra e checklist de validacao do rodape HML.
