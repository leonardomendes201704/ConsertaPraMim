# ST-016 - Concessao administrativa de creditos e notificacao de premio

Status: In Progress  
Epic: EPIC-006

## Objetivo

Como admin, quero conceder creditos para prestadores e notifica-los imediatamente sobre o premio para reconhecer performance, resolver incidentes comerciais ou executar campanhas.

## Criterios de aceite

- Admin consegue conceder credito informando:
  - prestador;
  - valor;
  - motivo;
  - tipo da concessao (`Premio`, `Campanha`, `Ajuste`);
  - data limite de uso (opcional/obrigatoria conforme regra).
- Admin consegue estornar credito ainda nao consumido (total/parcial, conforme regra definida).
- Operacoes de concessao/estorno sao protegidas por `AdminOnly`.
- Concessao gera notificacao para prestador:
  - persistida no centro de notificacoes;
  - enviada em tempo real via SignalR;
  - com mensagem clara de premio recebido e impacto financeiro.
- Tela admin permite consultar historico de premios por prestador.
- Toda concessao/estorno gera auditoria com before/after.

## Tasks

- [x] Definir regras de negocio para concessao, vigencia e estorno de credito.
- [x] Implementar API Admin para conceder credito com validacao de payload.
- [x] Implementar API Admin para estorno/ajuste de credito concedido.
- [ ] Implementar UI no portal admin para conceder e consultar creditos por prestador.
- [ ] Integrar notificacao persistente + SignalR para premio concedido.
- [x] Padronizar template de mensagem de premio para prestador.
- [ ] Incluir filtros operacionais (prestador, periodo, tipo, status).
- [x] Adicionar trilha de auditoria detalhada no admin.
- [x] Criar testes unitarios dos fluxos de concessao/estorno.
- [ ] Criar testes de integracao da API e da notificacao de premio.

## Regras de negocio definidas (2026-02-16)

- Tipos de concessao:
  - `Premio`: expiracao opcional; quando ausente, default de 90 dias.
  - `Campanha`: expiracao obrigatoria, futura e limitada a 365 dias.
  - `Ajuste`: expiracao opcional.
- Estorno administrativo:
  - executado como debito no ledger para remover credito nao consumido;
  - falha com `insufficient_balance` quando o saldo atual e menor que o valor solicitado.
- Validacoes de payload:
  - `ProviderId` deve ser de usuario `Provider` ativo;
  - `Amount` deve ser maior que zero;
  - `Reason` obrigatorio.
- Auditoria administrativa:
  - eventos `AdminProviderCreditGrantExecuted` e `AdminProviderCreditReversalExecuted`;
  - metadata com snapshot `before/after` de saldo e detalhes da operacao.
- Notificacao:
  - template padronizado por tipo de concessao (`Premio`, `Campanha`, `Ajuste`);
  - envio em tempo real pelo canal de notificacao (`SignalR`).

## Entregas tecnicas (incremento backend)

- Novos endpoints admin:
  - `POST /api/admin/provider-credits/grants`
  - `POST /api/admin/provider-credits/reversals`
- Novo servico de aplicacao: `AdminProviderCreditService`
- Novos DTOs administrativos de credito:
  - `AdminProviderCreditGrantRequestDto`
  - `AdminProviderCreditReversalRequestDto`
  - `AdminProviderCreditMutationResultDto`
- Novo enum de dominio: `ProviderCreditGrantType`
- Cobertura unitaria inicial:
  - `AdminProviderCreditServiceTests` (regras de campanha, sucesso com notificacao/auditoria, falha por saldo insuficiente)
