# ST-002 - Login com e-mail/senha e autenticacao na API para app cliente web

Status: Done
Epic: EPIC-002

## Objetivo

Trocar a tela de login do app cliente para e-mail/senha e autenticar de forma real contra a API principal do ConsertaPraMim.

## Criterios de aceite

- Tela de auth nao usa mais telefone/codigo.
- Campos obrigatorios: e-mail e senha.
- Tela abre com credenciais default do Cliente 02 ja preenchidas.
- Botao Entrar chama `POST /api/auth/login`.
- Em sucesso, sessao (token + usuario) e salva localmente.
- Ao iniciar app, sessao salva so abre dashboard se estiver estruturalmente valida (role Client + token nao expirado).
- Se sessao ausente/invalida/expirada, app abre direto em login (AUTH).
- Em erro, app mostra mensagem amigavel de autenticacao.
- Ao abrir Auth, app verifica `/health` da API automaticamente.
- Se API indisponivel, app exibe tela amigavel de manutencao.
- Tela de manutencao mostra codigo tecnico para suporte/DEV.
- Apenas role `Client` pode entrar nesse app.
- Logout limpa sessao local e retorna para auth.
- API permite CORS para o app local.

## Tasks

- [x] Criar servico de autenticacao no app para login, persistencia e limpeza de sessao.
- [x] Definir tipo de sessao autenticada no `types.ts`.
- [x] Criar tipagem de ambiente (`vite-env.d.ts`) para `VITE_API_BASE_URL`.
- [x] Refatorar componente `Auth` para e-mail/senha e tratamento de erro/loading.
- [x] Pre-preencher login default com Cliente 02 (com override por variavel de ambiente).
- [x] Implementar health-check da API ao entrar na tela de Auth.
- [x] Exibir tela amigavel de manutencao quando API indisponivel.
- [x] Criar catalogo de codigos de indisponibilidade/autenticacao para troubleshooting.
- [x] Integrar `Auth` com endpoint real `/api/auth/login`.
- [x] Persistir sessao em `localStorage` e reaplicar ao iniciar app.
- [x] Integrar logout para limpar sessao local.
- [x] Validar sessao salva no bootstrap do app (exp do JWT + role Client) antes de abrir dashboard.
- [x] Redirecionar para login quando sessao salva estiver ausente/invalida/expirada.
- [x] Propagar nome/e-mail autenticado para tela de perfil.
- [x] Ajustar CORS da API para `localhost:3000` e `localhost:3001`.
- [x] Atualizar README do app com variavel `VITE_API_BASE_URL` e dependencia da API.
- [x] Criar diagrama de fluxo e diagrama de sequencia da funcionalidade.

## Atualizacao de implementacao (2026-02-16)

### App cliente web

- Novo servico criado: `conserta-pra-mim app/services/auth.ts`.
  - `loginWithEmailPassword(email, password)`
  - `saveAuthSession(session)`
  - `loadAuthSession()`
  - `clearAuthSession()`
- Novo tipo `AuthSession` em `conserta-pra-mim app/types.ts`.
- Nova tipagem Vite env: `conserta-pra-mim app/vite-env.d.ts`.
- `conserta-pra-mim app/components/Auth.tsx` atualizado para:
  - e-mail e senha
  - prefill default: `cliente2@teste.com` / `SeedDev!2026`
  - loading no submit
  - exibir erro de autenticacao
  - health-check automatico ao abrir tela
  - fallback para tela de manutencao com codigo tecnico
- `conserta-pra-mim app/services/auth.ts` ampliado com:
  - `checkApiHealth()`
  - catalogo `API_ISSUE_CATALOG`
  - classe `AppApiError` com `code` para diagnostico
- `conserta-pra-mim app/App.tsx` atualizado para:
  - carregar sessao salva na inicializacao
  - abrir `AUTH` quando sessao salva for invalida/expirada
  - salvar sessao no login
  - limpar sessao no logout
  - enviar nome/e-mail autenticado para `Profile`
- `conserta-pra-mim app/services/auth.ts` atualizado para:
  - validar sessao salva com parse de `exp` do JWT
  - invalidar automaticamente sessao local expirada/inconsistente
- `conserta-pra-mim app/components/Profile.tsx` atualizado para receber `userName` e `userEmail` via props.
- `conserta-pra-mim app/README.md` atualizado com `VITE_API_BASE_URL`, `VITE_DEFAULT_LOGIN_EMAIL` e `VITE_DEFAULT_LOGIN_PASSWORD`.
- Catalogo de codigos publicado em:
  - `Documentacao/CONSUMER_APP_WEB/CODIGOS_INDISPONIBILIDADE_AUTENTICACAO_APP.md`

### API

- `Backend/src/ConsertaPraMim.API/Program.cs` atualizado no policy `WebApps` para incluir:
  - `http://localhost:3000`
  - `http://localhost:3001`
  - `https://localhost:3000`
  - `https://localhost:3001`
- `Backend/src/ConsertaPraMim.API/Program.cs` exposto endpoint de health:
  - `GET /health` (via `MapHealthChecks`), usado pelo app na entrada da tela de Auth.

## Diagramas

- Fluxo:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-002-login-email-senha-api/fluxo-login-email-senha-api.mmd`
- Sequencia:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-002-login-email-senha-api/sequencia-login-email-senha-api.mmd`
