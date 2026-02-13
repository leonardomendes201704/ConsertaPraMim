# ST-016 - Concessao administrativa de creditos e notificacao de premio

Status: Backlog  
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

- [ ] Definir regras de negocio para concessao, vigencia e estorno de credito.
- [ ] Implementar API Admin para conceder credito com validacao de payload.
- [ ] Implementar API Admin para estorno/ajuste de credito concedido.
- [ ] Implementar UI no portal admin para conceder e consultar creditos por prestador.
- [ ] Integrar notificacao persistente + SignalR para premio concedido.
- [ ] Padronizar template de mensagem de premio para prestador.
- [ ] Incluir filtros operacionais (prestador, periodo, tipo, status).
- [ ] Adicionar trilha de auditoria detalhada no admin.
- [ ] Criar testes unitarios dos fluxos de concessao/estorno.
- [ ] Criar testes de integracao da API e da notificacao de premio.
