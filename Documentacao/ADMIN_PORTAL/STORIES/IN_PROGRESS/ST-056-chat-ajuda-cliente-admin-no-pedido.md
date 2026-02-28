# ST-056 - Chat de ajuda cliente x admin no detalhe do pedido

Status: In Progress
Epic: EPIC-025

## Objetivo

Transformar a aba `Precisa de ajuda?` do detalhe do pedido em um canal contextual de suporte entre cliente e admin, preservando historico, anexos e navegacao pelo pedido vinculado.

## Criterios de aceite

- Cliente consegue abrir `ServiceRequests/Details/{id}`, entrar na aba `Precisa de ajuda?` e visualizar o historico do atendimento ligado ao pedido.
- Se nao houver chamado previo, o primeiro envio cria automaticamente um ticket de suporte contextualizado ao pedido.
- Cliente pode enviar texto e/ou anexos (`imagem`, `video`, `audio`, `documento`) no mesmo fluxo.
- Anexos ficam disponiveis no historico com preview em lightbox fullscreen quando houver suporte embutido; documentos sem preview devem abrir em nova guia.
- Portal admin passa a tratar o solicitante como `Cliente` quando o chamado vier dessa origem, preservando fila, status, atribuicao e auditoria existentes.
- O admin visualiza atalho para o pedido vinculado e consegue responder no mesmo ticket.
- A aba do cliente deve detectar novas mensagens do admin quando estiver aberta e refletir mudanca sem exigir navegacao manual fora do pedido.
- Documentacao operacional atualizada (Epic + Story + changelog + QA manual).

## Tasks

- [x] Criar Epic/Story da entrega e registrar a mudanca no changelog local.
- [x] Implementar service de aplicacao para localizar/criar ticket de ajuda do cliente por `serviceRequestId`.
- [x] Integrar a tela `ServiceRequests/Details` com timeline, envio de mensagem, anexos e lightbox fullscreen.
- [x] Ajustar a fila/tela admin para reconhecer chamados originados por cliente e visualizar anexos no mesmo padrao.
- [x] Adicionar polling leve na aba de ajuda para detectar novas mensagens e atualizar a tela.
- [x] Atualizar manual QA/Operacao com caso E2E da funcionalidade.
