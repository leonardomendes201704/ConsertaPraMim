# ST-008 - Matching de prestadores e agendamento multi-visitas

Status: Backlog  
Epic: EPIC-002

## Objetivo

Depois da abertura do pedido, listar prestadores elegiveis da area e permitir agendamento de ate 3 visitas em dias distintos, conduzido por conversa natural.

## Criterios de aceite

- Bot retorna lista de prestadores elegiveis para o pedido e regiao do cliente.
- Cliente pode pedir agendamento de 1 a 3 visitas em dias diferentes.
- Bot interpreta periodo/horario em linguagem natural (ex.: quarta de manha, sexta a tarde).
- Agendamentos sao criados via API com validacao de conflito e indisponibilidade.
- Cliente recebe confirmacao de cada visita solicitada e status final.
- Falhas de agenda geram alternativa clara (novo horario/prestador) no mesmo fluxo.

## Tasks

- [ ] Criar endpoint/servico na API para listar prestadores elegiveis por pedido e cobertura.
- [ ] Criar endpoint/servico de agendamento em lote (ate 3 visitas) com validacao de regras.
- [ ] Implementar parser de janela temporal em linguagem natural para datas/periodos.
- [ ] Implementar regra de limite de 3 visitas em dias distintos e feedback de violacao.
- [ ] Persistir no contexto conversacional as opcoes sugeridas e decisoes do cliente.
- [ ] Implementar respostas naturais de confirmacao, indisponibilidade e replanejamento.
- [ ] Criar testes unitarios/integracao para matching, conflitos e limite de visitas.
- [ ] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [ ] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.
