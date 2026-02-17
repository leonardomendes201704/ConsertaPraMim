# EPIC-009 - Revalidacao de dados a cada navegacao no app

Status: Done
Trilha: CONSUMER_APP_WEB

## Objetivo

Garantir consistencia de estado no app cliente sempre que o usuario navegar entre telas, incluindo voltar, reentrar e alternar contextos operacionais.

## Problema de negocio

- Em alguns fluxos, o app mostrava estado antigo ao voltar para telas ja visitadas.
- Usuario precisava forcar refresh manual para ver o estado real.
- Isso causava incoerencia no acompanhamento de pedidos, propostas e status.

## Resultado esperado

- Ao entrar em telas criticas, o app revalida dados automaticamente no backend.
- Retorno de tela (back) reflete estado real, sem depender de cache local desatualizado.
- Regras de refresh ficam centralizadas no `App.tsx`.

## Historia vinculada

- ST-011 - Refresh on enter para Dashboard, Pedidos, Detalhes e Proposta
