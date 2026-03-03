# ST-005 - Login do cliente no Telegram Bridge e vinculacao de conversa

Status: In Progress  
Epic: EPIC-002

## Objetivo

Adicionar autenticacao de cliente no `ConsertaPraMim.Web.TelegramBridge` com email/senha para vincular a conversa ao cliente correto.

## Criterios de aceite

- Tela de login com email/senha disponivel antes do acesso ao chat.
- Fluxo de autenticacao usa API oficial de identidade da plataforma.
- Sessao autenticada expira e redireciona para login quando invalida.
- Conversa e operacoes do bot usam `ClientId` derivado da sessao autenticada.
- Login direciona automaticamente para uma conversa unica cliente-chatbot; se nao existir, a conversa e criada sem input manual de `chatId`.
- Logout invalida sessao local e remove acesso ao chat.
- Testes cobrindo acesso anonimo, login valido e erro de credencial.

## Tasks

- [x] Implementar tela e controller de login no projeto `ConsertaPraMim.Web.TelegramBridge`.
- [x] Integrar autenticacao com endpoint de login existente na API (sem duplicar regra de senha).
- [x] Persistir token/sessao de forma segura (cookie com flags adequadas e expiracao).
- [x] Proteger rotas de chat com `[Authorize]` e redirecionamento para login.
- [x] Vincular `ClientId` da sessao aos calls da API do chatbot.
- [x] Garantir conversa unica por cliente com criacao automatica no primeiro acesso (sem `chatId` manual).
- [x] Implementar fluxo de logout e limpeza de contexto de sessao.
- [x] Criar testes unitarios/integracao para autenticao e autorizacao basica.
- [x] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [ ] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.
