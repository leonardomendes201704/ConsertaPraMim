# ST-029 - Autenticacao admin e sessao segura no mobile

Status: Done
Epic: EPIC-010

## Objetivo

Garantir login admin no app mobile com validacao de role, persistencia de sessao e tratamento de indisponibilidade da API.

## Criterios de aceite

- Login usa `/api/auth/login` e aceita apenas role `Admin`.
- Sessao e token persistem localmente e suportam logout.
- Tela de manutencao/indisponibilidade aparece quando health-check falhar.
- Erros de autenticacao sao exibidos com mensagem clara.

## Tasks

- [x] Implementar `services/auth.ts` com health-check, login e sessao.
- [x] Criar tela `Auth` com estados de loading/erro/manutencao.
- [x] Integrar login/sessao no `App.tsx`.
- [x] Cobrir fluxo de logout e expiracao de sessao.
