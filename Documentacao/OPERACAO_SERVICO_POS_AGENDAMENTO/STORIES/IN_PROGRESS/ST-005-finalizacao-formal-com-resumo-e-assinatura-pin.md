# ST-005 - Finalizacao formal com resumo e assinatura digital/PIN

Status: In Progress  
Epic: EPIC-001

## Objetivo

Concluir atendimento com aceite formal do cliente e comprovante de fechamento, reduzindo ambiguidades sobre entrega e escopo executado.

## Criterios de aceite

- Prestador gera resumo de conclusao com itens executados e observacoes.
- Cliente confirma conclusao por assinatura digital simples (nome) ou PIN de 4-6 digitos.
- Sem aceite formal do cliente, pedido fica em estado `Aguardando aceite de conclusao`.
- Ao aceitar, pedido transita para `Completed` e agenda fecha automaticamente.
- Comprovante de conclusao fica disponivel para cliente e prestador.
- Fluxo possui timeout e fallback para analise admin em impasse.

## Tasks

- [x] Criar novo estado de fechamento pendente de aceite do cliente.
- [x] Modelar entidade de termo de conclusao com hash e metadados.
- [x] Implementar geracao e validacao de PIN one-time com expiracao.
- [x] Implementar endpoint de confirmar conclusao por cliente.
- [x] Implementar endpoint de contestar conclusao com motivo.
- [x] Exibir tela de resumo e confirmacao no portal cliente.
- [x] Exibir recibo/comprovante no portal prestador e cliente.
- [x] Notificar admin automaticamente quando houver contestacao.
- [x] Criar testes de seguranca para tentativa de replay de PIN.
- [ ] Atualizar manual QA com roteiro de aceite e contestacao.
