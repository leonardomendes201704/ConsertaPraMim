# EPIC-002 - Login do app cliente com e-mail/senha e autenticacao na API

Status: Done
Trilha: CONSUMER_APP_WEB

## Objetivo

Substituir o login por telefone/codigo no app cliente web por autenticacao real via e-mail e senha, consumindo o endpoint oficial da API e mantendo sessao local para melhorar seguranca e aderencia ao dominio real do sistema.

## Resultado esperado

- Usuario faz login com e-mail e senha.
- App autentica no endpoint `POST /api/auth/login` da API oficial.
- Token JWT e dados do usuario ficam persistidos localmente.
- Sessao e reaproveitada ao reabrir o app.
- Logout remove a sessao local.
- CORS da API permite o app local (`localhost:3000/3001`).

## Historias vinculadas

- ST-002 - Login com e-mail/senha e autenticacao na API para app cliente web
