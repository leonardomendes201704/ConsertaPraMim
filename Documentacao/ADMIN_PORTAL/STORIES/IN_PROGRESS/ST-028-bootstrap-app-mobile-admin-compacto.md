# ST-028 - Bootstrap do app mobile admin compacto

Status: In Progress
Epic: EPIC-010

## Objetivo

Criar a base tecnica do app mobile admin no mesmo padrao dos apps cliente/prestador, com projeto compilavel para web e Android.

## Criterios de aceite

- Projeto `conserta-pra-mim-admin app` criado com estrutura padrao.
- App abre com fluxo inicial (splash + casca de navegacao).
- Configuracao Capacitor e Android pronta para build de APK.
- Build web (`npm run build`) e build backend relacionado sem erros de compilacao.

## Tasks

- [ ] Criar app `conserta-pra-mim-admin app` (React + Vite + Capacitor).
- [ ] Ajustar `package.json`, `capacitor.config.ts` e identificador Android do app Admin.
- [ ] Implementar estrutura base de tipos, servicos e componentes iniciais.
- [ ] Validar build local web do app.
