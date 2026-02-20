# ST-025 - Realtime, notificacoes e SLA basico de suporte

Status: Backlog  
Epic: EPIC-008

## Objetivo

Como operacao, quero notificacoes e atualizacao quase em tempo real dos chamados para reduzir tempo de resposta e evitar fila esquecida.

## Criterios de aceite

- Novo ticket criado gera aviso para o painel admin.
- Nova resposta do admin gera aviso para o prestador.
- Tela de detalhe atualiza mensagens sem refresh manual (quando possivel).
- Existe indicador de tempo sem resposta (SLA basico) na fila admin.
- Falhas de notificacao nao quebram o fluxo principal de atendimento.

## Tasks

- [ ] Definir estrategia de realtime (reuso de hub atual ou novo hub dedicado).
- [ ] Publicar eventos de ticket criado/mensagem/status alterado.
- [ ] Implementar consumo de eventos no portal admin.
- [ ] Implementar consumo de eventos no portal prestador.
- [ ] Implementar fallback de polling para ambientes sem websocket.
- [ ] Adicionar calculo de SLA basico (primeira resposta e ultima interacao).
- [ ] Criar testes de integracao/realtime com cenarios de reconexao.
- [ ] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [ ] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.
