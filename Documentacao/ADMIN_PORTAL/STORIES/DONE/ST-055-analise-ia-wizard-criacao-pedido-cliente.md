# ST-055 - Etapa "Analise do problema" no wizard de criacao de pedido do cliente

Status: Done
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
- O passo 3 resolve rua/bairro/cidade via CEP, persiste bairro no pedido e mostra mapa de referencia com pin + raio de 1km, incluindo aviso de que o endereco real e compartilhado no agendamento.
- Endpoint deve ser protegido e restrito ao papel `Client`.
- Resumo/highlights da analise sao persistidos no pedido e exibidos no detalhe para o prestador.
- Documentacao operacional atualizada (changelog + manual QA).

## Tasks

- [x] Criar EPIC/STORY/TASKS da entrega e registrar no board/changelog.
- [x] Implementar backend E2E da analise IA (DTOs, service, endpoint API e narrativa Swagger).
- [x] Implementar frontend E2E do wizard cliente com novo passo 2, feedback de analise e revisao em 4 etapas.
- [x] Atualizar QA manual, mover story para DONE e encerrar epic.
- [x] Evoluir passo 3 para resolver/persistir bairro e exibir mapa com raio operacional de 1km.
