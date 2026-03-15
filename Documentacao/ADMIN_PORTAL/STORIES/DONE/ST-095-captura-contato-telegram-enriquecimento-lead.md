# ST-095 - Captura de contato do Telegram e enriquecimento automatico do lead

Status: Done
Epic: EPIC-TELEGRAM-002

## Objetivo

Capturar telefone e, quando aplicavel, e-mail no primeiro atendimento do Telegram para enriquecer o lead do CPM Full e o contato do Chatwoot sem quebrar o fluxo conversacional ja publicado.

## Entrega realizada

- O bot passou a solicitar telefone com botao nativo `request_contact` logo apos o bootstrap do lead Telegram.
- O bridge agora aceita contato compartilhado pelo Telegram e fallback textual seguro para telefone/e-mail.
- O CPM Full atualiza o mesmo lead e o mesmo vinculo tecnico Telegram sem duplicar conversa ou apagar dados ja capturados.
- O detalhe do lead no Kanban passou a exibir o telefone capturado no vinculo Telegram em formato mascarado.
- A sincronizacao subsequente com Chatwoot reaproveita o contato tecnico existente e o enriquece com o telefone/e-mail real quando informado depois do primeiro contato.

## Criterios de aceite atendidos

- O bot consegue pedir telefone com botao nativo de compartilhamento de contato.
- O compartilhamento de telefone atualiza o lead existente no CPM Full, sem duplicar lead.
- O contato do Chatwoot e enriquecido com o telefone real quando ele for fornecido depois do bootstrap inicial.
- O fluxo continua funcional quando o usuario nao quiser compartilhar contato naquele momento.
- QA/manual/changelog foram atualizados no mesmo ciclo.

## Tasks

- [x] Criar Epic/Story da nova trilha e registrar a abertura no changelog.
- [x] Mapear o melhor momento da jornada para solicitar telefone/e-mail.
- [x] Implementar captura de telefone via `request_contact` e fallback textual.
- [x] Atualizar `TelegramBridge`, CPM Full e Chatwoot para enriquecer o mesmo lead/contato ja existente.
- [x] Cobrir testes de regressao e atualizar manual operacional da trilha.
