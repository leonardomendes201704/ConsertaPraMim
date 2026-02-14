# ST-011 - Pagamento integrado (PIX/cartao) e comprovantes

Status: Backlog  
Epic: EPIC-004

## Objetivo

Permitir pagamento do servico dentro da plataforma, com experiencia simples para cliente e comprovacao transparente para todas as partes.

## Criterios de aceite

- Cliente pode escolher PIX ou cartao ao concluir atendimento.
- Sistema registra estados `Pendente`, `Pago`, `Falhou`, `Estornado`.
- Comprovante de pagamento fica disponivel em cliente e prestador.
- Requisicoes de callback/webhook sao idempotentes.
- Admin visualiza trilha completa do pagamento no pedido.
- Em falha de pagamento, fluxo orienta tentativa de novo metodo.

## Tasks

- [ ] Definir provider de pagamento inicial e contrato de integracao.
- [ ] Criar entidade de transacao e estados financeiros por pedido.
- [ ] Implementar endpoint de criacao de checkout PIX/cartao.
- [ ] Implementar endpoint webhook com validacao de assinatura.
- [ ] Garantir idempotencia por `ProviderTransactionId`.
- [ ] Implementar comprovante em HTML/PDF simples no portal.
- [ ] Exibir status de pagamento nas telas de cliente/prestador/admin.
- [ ] Implementar fluxo de retry e troca de metodo de pagamento.
- [ ] Criar testes de integracao com cenarios de webhook.
- [ ] Criar monitoramento de falha por provider/canal.
