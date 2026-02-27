# ST-055 - Etapa "Analise do problema" no wizard de criacao de pedido do cliente

Status: In Progress
Epic: EPIC-024

## Objetivo

Inserir uma etapa intermediaria de analise IA entre `O que precisa?` e `Onde?` no fluxo de criacao de pedido do portal cliente, com resumo breve e operacional do entendimento do problema.

## Criterios de aceite

- O wizard `ServiceRequests/Create` passa a exibir 4 etapas com progresso correto.
- A etapa 2 (`Analise do problema`) dispara chamada ao backend com categoria + descricao e exibe:
  - resumo de entendimento;
  - highlights tecnicos curtos (quando houver);
  - indicacao de fallback quando OpenAI estiver indisponivel.
- Em erro de integracao, o usuario recebe feedback claro e pode tentar novamente sem perder os dados do passo 1.
- O passo 3 (`Onde?`) e o passo 4 (`Revisar`) continuam funcionando com validacao de CEP e revisao final.
- Endpoint deve ser protegido e restrito ao papel `Client`.
- Documentacao operacional atualizada (changelog + manual QA).

## Tasks

- [x] Criar EPIC/STORY/TASKS da entrega e registrar no board/changelog.
- [x] Implementar backend E2E da analise IA (DTOs, service, endpoint API e narrativa Swagger).
- [x] Implementar frontend E2E do wizard cliente com novo passo 2, feedback de analise e revisao em 4 etapas.
- [ ] Atualizar QA manual, mover story para DONE e encerrar epic.
