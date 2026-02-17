# Documentacao Completa - Conserta Pra Mim App (Web React)

## 1. Objetivo do app

O projeto `conserta-pra-mim app` e um front-end web com experiencia mobile-first para o cliente final da plataforma Conserta Pra Mim.

Objetivos principais:

- Permitir autenticacao basica do cliente.
- Abrir novos pedidos de servico com fluxo guiado equivalente ao portal do cliente.
- Acompanhar status de pedidos e detalhes do atendimento.
- Conversar com prestador via chat.
- Finalizar servico com pagamento e avaliacao.
- Centralizar notificacoes e gerenciamento de perfil.

## 2. Escopo atual (estado real)

Este app esta em formato de prototipo funcional de front-end com integracoes reais parciais com o backend principal.

Escopo implementado:

- Navegacao interna por estado (`AppState`) sem React Router.
- Login real com e-mail/senha na API (`POST /api/auth/login`).
- Health-check da API na tela de autenticacao (`GET /health`).
- Listagem real de pedidos do cliente na tela "Meus Pedidos" via endpoint mobile dedicado (`GET /api/mobile/client/orders`).
- Separacao real de pedidos em "Ativos" e "Historico" no payload da API mobile.
- Detalhes reais de pedido com fluxo e historico (`GET /api/mobile/client/orders/{orderId}`).
- Fluxo real de solicitacao de servico no app, com endpoints mobile dedicados:
  - `GET /api/mobile/client/service-requests/categories`
  - `GET /api/mobile/client/service-requests/zip-resolution`
  - `POST /api/mobile/client/service-requests`
- Resposta de chat simulada com Gemini no modulo de conversa.
- UI completa para dashboard, pedidos, perfil, chat, notificacoes e finalizacao.

Escopo nao implementado:

- Persistencia full de todas as telas (auth, pedidos e solicitacao estao integrados; demais modulos ainda locais/parciais).
- Upload real de anexos no chat.
- Chat realtime integrado ao backend.
- Notificacoes reais integradas ao backend.
- Controle completo de permissao por contexto de tela.

## 3. Stack tecnica

- Runtime: Node.js
- Front-end: React 19 (`react`, `react-dom`)
- Build/Dev Server: Vite 6
- Linguagem: TypeScript
- Estilo: Tailwind via CDN + utilitarios inline
- Icones: Material Symbols (Google Fonts)
- IA: SDK `@google/genai` (Gemini)

Arquivo de referencia:

- `conserta-pra-mim app/package.json`

## 4. Estrutura de pastas

Base: `c:\Leonardo\Labs\ConsertaPraMimWeb\conserta-pra-mim app`

Arquivos principais:

- `App.tsx`: orquestrador de estados/telas, sessao e carregamento de pedidos via API.
- `types.ts`: contratos de tipos do app (pedidos, notificacoes, mensagens).
- `services/mobileServiceRequests.ts`: integracao do wizard de solicitacao (categorias, CEP, criacao).
- `services/gemini.ts`: integracoes com Gemini (diagnostico e chat).
- `components/`: telas/componentes por funcionalidade.
- `vite.config.ts`: configuracao de build e injecao de variaveis de ambiente.
- `index.html`: bootstrap do app e configuracao de tema/estilos base.
- `index.tsx`: montagem React no `#root`.

## 5. Maquina de estados e navegacao

Estados definidos em `types.ts`:

- `SPLASH`
- `ONBOARDING`
- `AUTH`
- `DASHBOARD`
- `NEW_REQUEST`
- `REQUEST_DETAILS`
- `CHAT_LIST`
- `CHAT`
- `CATEGORIES`
- `ORDERS`
- `PROFILE`
- `FINISH_SERVICE`
- `NOTIFICATIONS`

Fluxo principal:

1. Splash (2.5s) -> Onboarding.
2. Onboarding -> Auth.
3. Auth -> Dashboard.
4. Dashboard abre modulos (novo pedido, pedidos, perfil, chat, notificacoes).
5. Request Details -> Finish Service -> retorno Dashboard.

Referencia:

- `conserta-pra-mim app/App.tsx`

## 6. Componentes e funcionalidades

### 6.1 Splash

Arquivo:

- `conserta-pra-mim app/components/SplashScreen.tsx`

Comportamento:

- Exibe loading visual com progresso pseudo-aleatorio.
- Marca visual Brasil e branding do app.

### 6.2 Onboarding

Arquivo:

- `conserta-pra-mim app/components/Onboarding.tsx`

Comportamento:

- Carrossel com 3 slides (beneficios do app).
- Botao "Pular" e botao "Proximo/Comecar".

### 6.3 Auth

Arquivo:

- `conserta-pra-mim app/components/Auth.tsx`

Comportamento:

- Captura e-mail e senha.
- Abre com prefill default de credenciais de seed (`cliente2@teste.com` / `SeedDev!2026`), com override por ambiente.
- Executa health-check automatico (`GET /health`) ao entrar na tela.
- Em indisponibilidade da API, renderiza tela amigavel de manutencao.
- Exibe codigo tecnico para troubleshooting de DEV/QA.
- Chama autenticacao real via `POST /api/auth/login`.
- Exibe feedback de erro em caso de credenciais invalidas.
- Persiste sessao local (`localStorage`) com token JWT e dados do usuario.
- Reaproveita sessao salva ao reabrir o app.

### 6.4 Dashboard

Arquivo:

- `conserta-pra-mim app/components/Dashboard.tsx`

Comportamento:

- Header com perfil e sino de notificacoes.
- CTA principal "Pedir um Servico".
- Carrossel rapido de categorias.
- Lista de pedidos ativos/recentes.
- Bottom nav (Inicio/Pedidos/Chat/Perfil).

### 6.5 CategoryList

Arquivo:

- `conserta-pra-mim app/components/CategoryList.tsx`

Comportamento:

- Lista real de categorias ativas vindas da API mobile dedicada.
- Busca local por nome sobre as categorias retornadas.
- Estado de loading/erro com acao de retry.
- Selecao redireciona para fluxo de novo pedido.

### 6.6 ServiceRequestFlow

Arquivo:

- `conserta-pra-mim app/components/ServiceRequestFlow.tsx`

Comportamento:

- Etapa 1: categoria + descricao do problema.
- Etapa 2: CEP e preenchimento automatico de endereco (rua/cidade).
- Etapa 3: revisao e publicacao do chamado.
- Etapa 4: confirmacao de pedido enviado (protocolo/status).

Observacao tecnica:

- Fluxo principal alinhado ao portal do cliente: `1 -> 2 -> 3`.
- Etapa 4 existe apenas como tela de sucesso no app apos criacao.

### 6.7 RequestDetails

Arquivo:

- `conserta-pra-mim app/components/RequestDetails.tsx`

Comportamento:

- Mostra status operacional do pedido.
- Carrega detalhes reais do pedido em endpoint mobile dedicado.
- Exibe fluxo operacional por etapas (`flowSteps`).
- Exibe historico de eventos do pedido em timeline cronologica (`timeline`).
- Permite retry em erro de carga do historico.
- Permite abrir chat e iniciar finalizacao do servico.

### 6.8 ChatList e Chat

Arquivos:

- `conserta-pra-mim app/components/ChatList.tsx`
- `conserta-pra-mim app/components/Chat.tsx`

Comportamento:

- Lista de conversas por pedido.
- Tela de chat com historico local.
- Simulacao de digitacao e resposta do prestador via Gemini.

### 6.9 OrdersList

Arquivo:

- `conserta-pra-mim app/components/OrdersList.tsx`

Comportamento:

- Tabs "Ativos" e "Historico".
- Consome dados reais da API mobile dedicada.
- Recebe listas separadas por status finalizado/nao finalizado.
- Mostra estado de loading e erro com acao de retry.
- Cards clicaveis para detalhes do pedido.

### 6.10 Profile

Arquivo:

- `conserta-pra-mim app/components/Profile.tsx`

Comportamento:

- Edicao local de dados pessoais.
- CEP com lookup simulado.
- Preferencias de disponibilidade (manha/tarde/noite).
- Acao de logout local.

### 6.11 ServiceCompletionFlow

Arquivo:

- `conserta-pra-mim app/components/ServiceCompletionFlow.tsx`

Comportamento:

- Passo 1: confirmar conclusao.
- Passo 2: selecionar pagamento e valor.
- Passo 3: avaliar servico (estrelas + comentario).

### 6.12 Notifications

Arquivo:

- `conserta-pra-mim app/components/Notifications.tsx`

Comportamento:

- Lista notificacoes por tipo (`STATUS`, `MESSAGE`, `PROMO`, `SYSTEM`).
- Marcar como lida ao clicar.
- Limpar todas.

## 7. Modelo de dados local

Tipos principais (`types.ts`):

- `ServiceRequest`
- `Notification`
- `Message`
- `ServiceCategory`
- `ServiceRequestCategoryOption`
- `ChatPreview`

Persistencia atual:

- Sessao de autenticacao salva em `localStorage` (`token`, `userId`, `email`, `role`).
- Pedidos da tela "Meus Pedidos" carregados da API a cada login/restauracao de sessao.
- Demais dados ainda em `useState` local.

## 8. Integracao com IA (Gemini)

Arquivo:

- `conserta-pra-mim app/services/gemini.ts`

Funcoes:

- `getAIDiagnostic(userDescription)`
  - Gera resumo tecnico, possiveis causas e instrucoes de seguranca.
  - Forca resposta JSON via `responseSchema`.
- `getProviderReply(providerName, serviceTitle, userMessage, history)`
  - Simula resposta de chat do prestador.

Modelos usados:

- `gemini-3-flash-preview`

Requisito de ambiente:

- `GEMINI_API_KEY` em `.env.local`

## 9. Configuracao e execucao local

Pre-requisitos:

- Node.js instalado.

Passos:

1. Ir para pasta do app:
   - `cd "c:\Leonardo\Labs\ConsertaPraMimWeb\conserta-pra-mim app"`
2. Instalar dependencias:
   - `npm install`
3. Configurar chave Gemini no arquivo `.env.local`:
   - `GEMINI_API_KEY=<sua_chave>`
4. Subir ambiente dev:
   - `npm run dev`
5. Build de producao:
   - `npm run build`
6. Preview local do build:
   - `npm run preview`

## 10. Configuracoes de build relevantes

Arquivo:

- `conserta-pra-mim app/vite.config.ts`

Pontos relevantes:

- Porta default: `3000`.
- Host: `0.0.0.0`.
- Injecao de chave em build via `define`:
  - `process.env.API_KEY`
  - `process.env.GEMINI_API_KEY`

## 11. Riscos e pontos de atencao

### 11.1 Seguranca de chave API

Risco:

- Chave de IA em app cliente pode ser exposta no bundle/browser.

Recomendacao:

- Mover chamadas Gemini para backend (BFF/API) e nunca expor chave no front.

### 11.2 Dependencia da API para login e pedidos/solicitacoes

Risco:

- Sem API ativa, login, listagem e abertura real de pedidos nao funcionam.

Recomendacao:

- Garantir API disponivel em `VITE_API_BASE_URL`, monitorar `/health`, endpoints mobile e evoluir para refresh token.

### 11.3 Dados mockados residuais

Risco:

- Nem todas as telas estao integradas ao backend (ex.: notificacoes, chat, partes do dashboard).

Recomendacao:

- Avancar integracao dos modulos restantes com APIs reais dedicadas ao app.

### 11.4 Qualidade de texto/encoding

Observacao:

- Existem textos com caracteres corrompidos em alguns componentes (ex.: `Ã§`, `Ã¡`, `HistÃ³rico`).

Recomendacao:

- Padronizar todos os arquivos para UTF-8 e revisar strings PT-BR.

### 11.5 Dependencia de CDN

Risco:

- `tailwindcss.com` e fontes via CDN afetam disponibilidade, performance e CSP.

Recomendacao:

- Internalizar assets e pipeline CSS local para ambientes controlados.

## 12. Gap analysis com backend oficial

Para tornar este app totalmente produtivo, ainda faltam integracoes com:

- Chat real (com SignalR/WebSocket).
- Notificacoes reais (push/realtime).
- Pagamentos reais.
- Perfil real (persistencia e validacao).

## 13. Roadmap recomendado (prioridade)

P0 (obrigatorio para producao):

- API gateway para IA (retirar chave do front).
- Fechar integracao de notificacoes e chat.
- Correcao UTF-8 total.

P1:

- Persistencia offline parcial (`localStorage`/cache).
- Instrumentacao de logs de front.
- Melhorias de acessibilidade e testes automatizados.

P2:

- Evoluir para app mobile real (React Native/.NET MAUI).
- Push notifications nativas.

## 14. Criterios de aceite para considerar "pronto para producao"

- Nenhuma chave secreta exposta no front.
- Login real integrado e validado.
- Dados 100% vindos de backend oficial.
- Fluxos E2E criticos aprovados em QA.
- Textos PT-BR sem corrupcao de encoding.

## 15. Referencias de arquivo

- `conserta-pra-mim app/App.tsx`
- `conserta-pra-mim app/types.ts`
- `conserta-pra-mim app/services/auth.ts`
- `conserta-pra-mim app/services/mobileOrders.ts`
- `conserta-pra-mim app/services/mobileServiceRequests.ts`
- `conserta-pra-mim app/services/gemini.ts`
- `conserta-pra-mim app/components/Auth.tsx`
- `conserta-pra-mim app/components/Dashboard.tsx`
- `conserta-pra-mim app/components/ServiceRequestFlow.tsx`
- `conserta-pra-mim app/components/RequestDetails.tsx`
- `conserta-pra-mim app/components/ChatList.tsx`
- `conserta-pra-mim app/components/Chat.tsx`
- `conserta-pra-mim app/components/OrdersList.tsx`
- `conserta-pra-mim app/components/Profile.tsx`
- `conserta-pra-mim app/components/ServiceCompletionFlow.tsx`
- `conserta-pra-mim app/components/Notifications.tsx`
- `conserta-pra-mim app/package.json`
- `conserta-pra-mim app/vite.config.ts`

## 16. Diagramas

- Fluxo de navegacao:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-001-visao-geral-fluxos/fluxo-navegacao-app.mmd`
- Sequencia IA (diagnostico e chat):
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-001-visao-geral-fluxos/sequencia-ia-diagnostico-chat.mmd`
- Fluxo login + health-check/manutencao:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-002-login-email-senha-api/fluxo-login-email-senha-api.mmd`
- Sequencia login + codigos tecnicos:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-002-login-email-senha-api/sequencia-login-email-senha-api.mmd`
- Fluxo pedidos mobile dedicados:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-003-meus-pedidos-api-mobile/fluxo-meus-pedidos-api-mobile.mmd`
- Sequencia pedidos mobile dedicados:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-003-meus-pedidos-api-mobile/sequencia-meus-pedidos-api-mobile.mmd`
- Fluxo detalhes de pedido com historico:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-004-detalhes-pedido-fluxo-historico/fluxo-detalhes-pedido-historico.mmd`
- Sequencia detalhes de pedido com historico:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-004-detalhes-pedido-fluxo-historico/sequencia-detalhes-pedido-historico.mmd`
- Fluxo solicitacao de servico (paridade portal):
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-005-solicitacao-servico-paridade-portal/fluxo-solicitacao-servico-app-paridade-portal.mmd`
- Sequencia solicitacao de servico (paridade portal):
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-005-solicitacao-servico-paridade-portal/sequencia-solicitacao-servico-app-paridade-portal.mmd`
- Catalogo de codigos:
  - `Documentacao/CONSUMER_APP_WEB/CODIGOS_INDISPONIBILIDADE_AUTENTICACAO_APP.md`

## 17. Data da revisao

- 2026-02-17
