# ST-001 - Status operacional do prestador (Ausente, Online, EmAtendimento)

Status: Done  
Epic: EPIC-001

## Objetivo

Permitir que o prestador altere seu status operacional manualmente, com persistencia e atualizacao em tempo real para todos os pontos relevantes da aplicacao.

## Criterios de aceite

- Existe um campo de status operacional no dominio do prestador.
- O prestador pode alternar entre `Ausente`, `Online` e `EmAtendimento`.
- O status e persistido e reaplicado apos novo login/reload.
- Mudanca de status propaga via SignalR sem refresh de pagina.
- Cliente e prestador visualizam o status atualizado em ate 2 segundos.
- Regras de autorizacao impedem usuario nao-prestador de alterar status de prestador.
- Testes automatizados cobrem casos de sucesso e falha.

## Tasks

- [x] Definir enum `ProviderOperationalStatus` no dominio.
- [x] Adicionar propriedade de status operacional em `ProviderProfile`.
- [x] Criar migration e aplicar mapeamento EF para o novo campo.
- [x] Atualizar DTOs e contratos de perfil para expor status operacional.
- [x] Implementar endpoint/API para leitura e atualizacao do status.
- [x] Implementar publicacao de evento no SignalR ao mudar status.
- [x] Atualizar UI no portal do prestador com seletor/toggle de status.
- [x] Atualizar UI no portal do cliente para exibir status do prestador quando aplicavel.
- [x] Adicionar testes unitarios para regras de negocio/autorizacao.
- [x] Adicionar testes de integracao para persistencia e propagacao do status.
