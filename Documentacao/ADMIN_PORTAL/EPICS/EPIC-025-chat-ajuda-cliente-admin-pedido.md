# EPIC-025 - Chat de ajuda entre cliente e admin vinculado ao pedido

Status: In Progress
Trilha: CLIENTE_WEB, ADMIN_PORTAL

## Objetivo

Permitir que o cliente abra e acompanhe uma conversa de suporte com o time admin diretamente no detalhe do pedido, sem sair do contexto operacional do chamado.

## Problema de negocio

- Quando o cliente precisa de ajuda durante o ciclo do pedido, hoje ele nao possui um canal contextualizado dentro do proprio detalhe do servico.
- O time admin perde contexto ao atender ocorrencias fora do pedido, aumentando tempo de resposta e risco de orientar a partir de informacao incompleta.
- Evidencias (fotos, documentos e videos) precisam acompanhar a conversa para acelerar triagem, mediacao e decisao operacional.

## Resultado esperado

- A aba `Precisa de ajuda?` em `ServiceRequests/Details/{id}` passa a exibir um historico de conversa cliente x admin vinculado ao pedido.
- O cliente pode enviar mensagem com anexos, visualizar historico e abrir anexos em lightbox fullscreen.
- O admin recebe o mesmo chamado na fila de suporte existente, com identificacao de `Cliente` como solicitante e atalho para o pedido vinculado.
- O fluxo reutiliza a esteira de suporte existente, reduzindo custo de implementacao e mantendo auditoria/notificacao centralizadas.

## Metricas de sucesso

- Reducao do tempo medio de triagem de chamados iniciados pelo cliente.
- Maior taxa de resolucao sem necessidade de contato fora da plataforma.
- Aumento da completude de evidencias anexadas no primeiro contato.

## Escopo

### Inclui

- Reuso do dominio `SupportTickets` para chamados originados no portal cliente.
- UI cliente na aba `Precisa de ajuda?` com timeline, composer e anexos.
- Ajustes no portal admin para distinguir `Cliente` x `Prestador` como solicitante.
- Preview fullscreen de anexos nos dois lados.
- Registro documental (Epic, Story, changelog e manual QA).

### Nao inclui

- Novo hub realtime dedicado.
- Canal de suporte entre prestador e cliente.
- Suporte mobile nativo nesta entrega.

## Historias vinculadas

- ST-056 - Chat de ajuda cliente x admin no detalhe do pedido.
