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

## Codigos do fluxo de solicitacao de servico (mobile)

| Codigo | Contexto | Significado | Acao recomendada (DEV) |
|---|---|---|---|
| `CPM-REQ-001` | Categorias/CEP/Criacao | Falha de conexao ao chamar endpoints mobile de solicitacao de servico. | Verificar API online, CORS, SSL e `VITE_API_BASE_URL`. |
| `CPM-REQ-002` | Categorias/CEP/Criacao | Timeout na chamada do endpoint mobile. | Validar latencia da API, gargalo no backend ou indisponibilidade parcial. |
| `CPM-REQ-400` | CEP/Criacao | Erro de validacao funcional (categoria/descricao/CEP). | Revisar payload enviado e regras de validacao da API mobile. |
| `CPM-REQ-401` | Categorias/CEP/Criacao | Sessao expirada ou token invalido. | Reautenticar usuario e revisar emissao/expiracao do JWT. |
| `CPM-REQ-403` | Categorias/CEP/Criacao | Role sem permissao no endpoint mobile. | Garantir role `Client` no token. |
| `CPM-REQ-404` | CEP | CEP nao encontrado/geocodificacao indisponivel. | Validar CEP informado e integracao de geocoding. |
| `CPM-REQ-4XX` | Categorias/CEP/Criacao | Erro 4xx nao mapeado especificamente. | Inspecionar status HTTP e mensagem do backend. |
| `CPM-REQ-5XX` | Categorias/CEP/Criacao | Erro interno no servico de solicitacao. | Verificar logs da API e dependencias (DB/servicos). |

## Onde esta implementado

- Catalogo em codigo:
  - `conserta-pra-mim app/services/auth.ts`
  - `conserta-pra-mim app/services/mobileServiceRequests.ts`
- Tela de manutencao amigavel:
  - `conserta-pra-mim app/components/Auth.tsx`

## Ultima revisao

- Data: 2026-02-17
