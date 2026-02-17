# ST-005 - Fluxo de solicitar servico no app com paridade ao portal e endpoints mobile dedicados

Status: Done
Epic: EPIC-004

## Objetivo

Substituir o fluxo mockado de abertura de pedido no app por um wizard real, equivalente ao portal do cliente, com APIs exclusivas para canal mobile.

## Mapeamento do fluxo do portal do cliente

### Portal Web (referencia)

1. Tela `ServiceRequests/Create` carrega categorias ativas.
2. Etapa 1: cliente escolhe categoria e descreve o problema.
3. Etapa 2: cliente informa CEP e a tela resolve endereco automaticamente.
4. Etapa 3: cliente revisa dados e publica o chamado.
5. Backend cria pedido com validacoes de negocio e inicia fluxo operacional.

### App (implementado)

1. App carrega categorias ativas via endpoint mobile dedicado.
2. Etapa 1: escolha de categoria + descricao.
3. Etapa 2: resolucao de CEP via endpoint mobile dedicado.
4. Etapa 3: revisao + publicacao do chamado via endpoint mobile dedicado.
5. App exibe sucesso com protocolo e atualiza lista de pedidos.

## Criterios de aceite

- Existe contrato mobile dedicado para criacao de pedido.
- App nao depende de dados mockados para categoria/CEP/criacao.
- Wizard do app segue as 3 etapas equivalentes ao portal.
- Validacoes minimas no app e no backend:
  - categoria obrigatoria;
  - descricao minima;
  - CEP valido com 8 digitos.
- Em `401/403`, app trata sessao/perfil invalido.
- Em indisponibilidade/API erro, app mostra mensagem amigavel.

## Tasks

- [x] Mapear fluxo atual do portal (categoria, CEP, revisao/publicacao).
- [x] Criar DTOs mobile para fluxo de solicitacao de servico.
- [x] Criar interface `IMobileClientServiceRequestService`.
- [x] Implementar `MobileClientServiceRequestService` com:
  - [x] categorias ativas;
  - [x] resolucao de CEP;
  - [x] criacao de pedido.
- [x] Criar controller `MobileClientServiceRequestsController` com documentacao Swagger detalhada.
- [x] Registrar servico no `DependencyInjection`.
- [x] Criar service front `mobileServiceRequests.ts` para consumo dos endpoints mobile.
- [x] Refatorar `CategoryList.tsx` para usar categorias reais da API.
- [x] Refatorar `ServiceRequestFlow.tsx` para wizard real 3 etapas.
- [x] Atualizar `App.tsx` para passar sessao ao fluxo/categorias e sincronizar pedidos apos criacao.
- [x] Executar build backend e app.
- [x] Atualizar documentacao e diagramas.

## Arquivos impactados

### Backend

- `Backend/src/ConsertaPraMim.Application/DTOs/MobileClientServiceRequestDTOs.cs`
- `Backend/src/ConsertaPraMim.Application/Interfaces/IMobileClientServiceRequestService.cs`
- `Backend/src/ConsertaPraMim.Application/Services/MobileClientServiceRequestService.cs`
- `Backend/src/ConsertaPraMim.Application/DependencyInjection.cs`
- `Backend/src/ConsertaPraMim.API/Controllers/MobileClientServiceRequestsController.cs`

### App

- `conserta-pra-mim app/services/mobileServiceRequests.ts`
- `conserta-pra-mim app/components/ServiceRequestFlow.tsx`
- `conserta-pra-mim app/components/CategoryList.tsx`
- `conserta-pra-mim app/App.tsx`
- `conserta-pra-mim app/types.ts`

## Diagramas

- Fluxo:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-005-solicitacao-servico-paridade-portal/fluxo-solicitacao-servico-app-paridade-portal.mmd`
- Sequencia:
  - `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-005-solicitacao-servico-paridade-portal/sequencia-solicitacao-servico-app-paridade-portal.mmd`
