# ST-009 - Solicitacao de aditivo de escopo e valor pelo prestador

Status: In Progress  
Epic: EPIC-003

## Objetivo

Permitir que o prestador formalize mudancas de escopo/preco durante o servico, com justificativa clara e base de evidencia para decisao do cliente.

## Criterios de aceite

- Prestador pode abrir solicitacao de aditivo durante atendimento ativo.
- Solicitacao exige motivo, descricao do escopo adicional e valor incremental.
- Sistema permite anexar fotos/videos para justificar o aditivo.
- Cliente recebe notificacao imediata com resumo e valor proposto.
- Pedido fica bloqueado para conclusao enquanto aditivo estiver pendente.
- Historico de aditivos registra versao, autor e timestamp.

## Tasks

- [x] Criar entidade `ServiceScopeChangeRequest` com versao e status.
- [x] Criar endpoint para prestador abrir solicitacao de aditivo.
- [x] Validar limites de valor por plano/politica comercial.
- [ ] Integrar anexos de evidencia na solicitacao.
- [ ] Notificar cliente com CTA de aprovar/rejeitar.
- [ ] Exibir timeline de aditivos na tela de detalhes do pedido.
- [ ] Bloquear finalizacao do servico com aditivo pendente.
- [ ] Registrar trilha de auditoria completa do aditivo.
- [ ] Criar testes de autorizacao e regras de estado.
- [ ] Atualizar manual de operacao com fluxo de aditivo.
