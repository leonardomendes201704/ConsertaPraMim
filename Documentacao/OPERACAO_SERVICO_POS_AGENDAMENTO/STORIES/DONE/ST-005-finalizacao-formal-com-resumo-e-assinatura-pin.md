# ST-005 - Finalizacao formal com resumo e assinatura digital/PIN

Status: Done  
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
- [x] Atualizar manual QA com roteiro de aceite e contestacao.

## Roteiro QA - Aceite e Contestacao

### Pre-condicoes

1. Pedido com proposta aceita e agendamento confirmado.
2. Prestador concluiu o atendimento (status operacional `Completed`), gerando termo pendente.
3. Cliente e prestador conseguem abrir `ServiceRequests/Details/{id}` em seus portais.

### Cenario 1 - Aceite por PIN (fluxo feliz)

1. No portal do cliente, validar exibicao do bloco `Resumo e aceite de conclusao`.
2. Selecionar metodo `PIN one-time`.
3. Informar PIN valido recebido na notificacao.
4. Clicar em `Confirmar conclusao`.
5. Resultado esperado:
   - mensagem de sucesso;
   - status do pedido muda para `Completed`;
   - comprovante aparece em cliente e prestador com metodo `PIN one-time` e data/hora de aceite.

### Cenario 2 - Aceite por assinatura (fluxo feliz)

1. No portal do cliente, no mesmo bloco de aceite, escolher `Assinatura por nome`.
2. Informar nome valido (>= 3 caracteres).
3. Confirmar conclusao.
4. Resultado esperado:
   - status final `Completed`;
   - comprovante exibe metodo `Assinatura por nome`;
   - campo `Assinado por` preenchido no recibo.

### Cenario 3 - PIN invalido

1. Selecionar metodo PIN e informar valor incorreto.
2. Confirmar.
3. Resultado esperado:
   - operacao bloqueada com erro de validacao;
   - pedido permanece `PendingClientCompletionAcceptance`;
   - contador de tentativas do termo e atualizado.

### Cenario 4 - Replay de PIN (seguranca)

1. Concluir com sucesso via PIN.
2. Repetir a validacao usando o mesmo PIN.
3. Resultado esperado:
   - segunda tentativa falha com `invalid_state`;
   - nenhum novo aceite e registrado;
   - pedido permanece `Completed` sem alteracoes extras.

### Cenario 5 - Contestacao com motivo

1. No bloco de aceite, clicar em `Contestar conclusao`.
2. Informar motivo valido (>= 5 caracteres).
3. Resultado esperado:
   - termo passa para `ContestedByClient`;
   - comprovante mostra motivo da contestacao;
   - cliente e prestador recebem notificacao de contestacao;
   - admins ativos recebem notificacao `Agendamento: contestacao para analise`.

### Cenario 6 - Contestacao invalida

1. Tentar contestar com motivo curto (< 5 caracteres) ou vazio.
2. Resultado esperado:
   - erro `contest_reason_required`;
   - termo nao muda de estado.
