# Catalogo de Codigos - Indisponibilidade e Autenticacao (App Cliente)

Este catalogo padroniza os codigos exibidos no app para facilitar suporte e troubleshooting em ambiente DEV/QA.

## Codigos de disponibilidade da API

| Codigo | Contexto | Significado | Acao recomendada (DEV) |
|---|---|---|---|
| `CPM-API-001` | Health-check | Falha de conexao com API (rede/CORS/SSL). | Verificar API online, certificado HTTPS, CORS e `VITE_API_BASE_URL`. |
| `CPM-API-002` | Health-check | Timeout ao verificar disponibilidade. | Validar desempenho da API, travamento de processo ou dependencia externa lenta. |
| `CPM-API-003` | Health-check | Endpoint `/health` retornou erro 5xx. | Inspecionar logs da API e estado de banco/migrations/dependencias. |
| `CPM-API-004` | Health-check | Endpoint `/health` retornou erro 4xx. | Revisar rota de health, proxy, regras de acesso e configuracao de ambiente. |
| `CPM-API-005` | Health-check | Resposta de health invalida/inesperada. | Confirmar implementacao de `MapHealthChecks("/health")` e comportamento HTTP. |

## Codigos de autenticacao

| Codigo | Contexto | Significado | Acao recomendada (DEV) |
|---|---|---|---|
| `CPM-AUTH-001` | Login | Falha de conexao ao chamar `/api/auth/login`. | Verificar API, CORS e URL base do app. |
| `CPM-AUTH-002` | Login | Timeout no endpoint de login. | Avaliar latencia, lock de DB ou degradacao do servidor. |
| `CPM-AUTH-003` | Login | Payload de sucesso sem token valido. | Revisar contrato da resposta `LoginResponse` na API. |
| `CPM-AUTH-4XX` | Login | Erro de requisicao 4xx generico. | Revisar payload enviado e validacoes do endpoint. |
| `CPM-AUTH-401` | Login | Credenciais invalidas. | Confirmar e-mail/senha seed ou fluxo de cadastro do usuario. |
| `CPM-AUTH-403` | Login | Perfil nao permitido no app cliente. | Garantir que o usuario tenha role `Client`. |
| `CPM-AUTH-5XX` | Login | Erro interno no endpoint de login. | Verificar logs da API e excecoes do `AuthService`. |

## Onde esta implementado

- Catalogo em codigo:
  - `conserta-pra-mim app/services/auth.ts`
- Tela de manutencao amigavel:
  - `conserta-pra-mim app/components/Auth.tsx`

## Ultima revisao

- Data: 2026-02-16
