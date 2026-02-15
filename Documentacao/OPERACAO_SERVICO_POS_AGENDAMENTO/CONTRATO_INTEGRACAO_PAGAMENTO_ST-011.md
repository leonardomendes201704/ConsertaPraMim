# CONTRATO DE INTEGRACAO DE PAGAMENTO - ST-011

## 1. Provider inicial

- Provider inicial definido: `Mock`.
- Motivacao: viabilizar evolucao incremental do fluxo financeiro sem bloquear as proximas historias (entidade de transacao, checkout real, webhook idempotente e comprovantes).
- Estrategia: contrato `provider-agnostic` em `Application`, com adapter concreto em `Infrastructure`.

## 2. Contrato tecnico (Application)

Interface: `ConsertaPraMim.Application.Interfaces.IPaymentService`

Operacoes:

1. `CreateCheckoutSessionAsync(PaymentCheckoutRequestDto, CancellationToken)`
2. `ValidateWebhookSignature(PaymentWebhookRequestDto)`
3. `ParseWebhookAsync(PaymentWebhookRequestDto, CancellationToken)`
4. `RefundAsync(PaymentRefundRequestDto, CancellationToken)`
5. `ReleaseFundsAsync(Guid serviceRequestId, CancellationToken)`

DTOs:

- `PaymentCheckoutRequestDto`
- `PaymentCheckoutSessionDto`
- `PaymentWebhookRequestDto`
- `PaymentWebhookEventDto`
- `PaymentRefundRequestDto`

Enums:

- `PaymentProvider`
- `PaymentMethod`
- `PaymentTransactionStatus`

## 3. Adapter inicial (Infrastructure)

Implementacao atual:

- `ConsertaPraMim.Infrastructure.Services.MockPaymentService`

Comportamento:

- Cria URL de checkout mock por metodo (`pix`/`card`).
- Gera `ProviderTransactionId` e `CheckoutReference` mock.
- Valida assinatura de webhook via secret configurado.
- Interpreta payload de webhook mock e normaliza para `PaymentWebhookEventDto`.
- Suporta refund e release de fundos em modo simulado.

## 4. Configuracao

Arquivo: `ConsertaPraMim.API/appsettings*.json`

Secao:

```json
"Payments": {
  "Provider": "Mock",
  "Mock": {
    "CheckoutBaseUrl": "https://checkout.consertapramim.com/pay",
    "WebhookSecret": "mock-secret",
    "SessionExpiryMinutes": 30
  }
}
```

## 5. Regras de compatibilidade

1. O contrato exposto em `Application` nao deve depender de SDK especifico de provider.
2. Qualquer provider real futuro (ex.: Mercado Pago, Stripe, Pagar.me) deve implementar o mesmo contrato.
3. Payload de webhook deve ser convertido para DTO normalizado antes de tocar regras de negocio.

## 6. Proxima evolucao prevista

1. Criar entidade de transacao financeira por pedido (`ST-011` task 2).
2. Expor endpoint de checkout PIX/cartao usando este contrato (`ST-011` task 3).
3. Expor endpoint de webhook com validacao e idempotencia (`ST-011` tasks 4 e 5).
