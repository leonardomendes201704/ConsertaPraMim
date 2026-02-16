# Documentacao Completa - Conserta Pra Mim App (Web React)

## 1. Objetivo do app

O projeto `conserta-pra-mim app` e um front-end web com experiencia mobile-first para o cliente final da plataforma Conserta Pra Mim.

Objetivos principais:

- Permitir autenticacao basica do cliente.
- Abrir novos pedidos de servico com suporte de diagnostico por IA.
- Acompanhar status de pedidos e detalhes do atendimento.
- Conversar com prestador via chat.
- Finalizar servico com pagamento e avaliacao.
- Centralizar notificacoes e gerenciamento de perfil.

## 2. Escopo atual (estado real)

Este app esta em formato de prototipo funcional de front-end, com dados simulados em memoria e sem integracao real com o backend principal da solution.

Escopo implementado:

- Navegacao interna por estado (`AppState`) sem React Router.
- Dados mockados de pedidos e notificacoes.
- Fluxo de pedido com uso de Gemini para diagnostico e resposta de chat.
- UI completa para dashboard, pedidos, perfil, chat, notificacoes e finalizacao.

Escopo nao implementado:

- Login real com token/JWT.
- Persistencia em banco.
- Consumo de APIs reais do backend ConsertaPraMim.API.
- Upload real de anexos no chat.
- Controle de permissao por perfis.

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

- `App.tsx`: orquestrador de estados/telas e dados in-memory.
- `types.ts`: contratos de tipos do app (pedidos, notificacoes, mensagens).
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

- Captura telefone com mascara `(99) 99999-9999`.
- Fluxo simplificado: submit faz login local (sem API real).

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

- Lista completa de categorias (18 itens).
- Busca local por nome.
- Selecao redireciona para fluxo de novo pedido.

### 6.6 ServiceRequestFlow

Arquivo:

- `conserta-pra-mim app/components/ServiceRequestFlow.tsx`

Comportamento:

- Etapa 1: descricao + sugestoes comuns por categoria.
- Etapa 3: diagnostico IA com resumo, causas e seguranca.
- Etapa 4: confirmacao de pedido enviado.

Observacao tecnica:

- O fluxo usa etapas `1 -> 3 -> 4` (nao existe etapa 2 no estado local).

### 6.7 RequestDetails

Arquivo:

- `conserta-pra-mim app/components/RequestDetails.tsx`

Comportamento:

- Mostra status operacional (andamento/concluido).
- Exibe card do prestador (avatar, nota, especialidade).
- Exibe diagnostico IA associado ao pedido.
- Exibe timeline operacional.
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
- `ChatPreview`

Persistencia atual:

- Somente `useState` no ciclo de vida da sessao.
- Sem `localStorage` e sem backend.

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

### 11.2 Sem autenticacao real

Risco:

- Fluxo de login nao valida usuario/sessao.

Recomendacao:

- Integrar com endpoint de auth da plataforma e JWT com refresh token.

### 11.3 Dados mockados

Risco:

- Nao representa comportamento de producao (status real, SLA, sincronizacao).

Recomendacao:

- Integrar dados de pedidos/notificacoes/chat com APIs reais.

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

Para tornar este app produtivo, faltam integracoes com:

- Auth/login/cadastro real.
- Pedidos (CRUD + historico + status).
- Chat real (com SignalR/WebSocket).
- Notificacoes reais (push/realtime).
- Pagamentos reais.
- Perfil real (persistencia e validacao).

## 13. Roadmap recomendado (prioridade)

P0 (obrigatorio para producao):

- Auth real + sessao.
- API gateway para IA (retirar chave do front).
- Integracao pedidos/notificacoes/chat.
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

## 17. Data da revisao

- 2026-02-16
