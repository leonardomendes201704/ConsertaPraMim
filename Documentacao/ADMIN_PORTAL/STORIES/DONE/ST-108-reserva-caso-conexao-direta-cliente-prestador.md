# ST-108 - Reserva do caso e conexao direta entre cliente e prestador

Status: Done
Epic: EPIC-JORNADA-001

## Objetivo

Fazer com que o primeiro prestador valido que aceitar reserve o caso e receba dados suficientes para entrar em contato diretamente com o cliente.

## Criterios de aceite

- O primeiro aceite valido reserva o caso.
- Os demais prestadores deixam de poder aceitar.
- Cliente e prestador recebem os dados necessarios para contato direto.
- O card do Kanban avanca automaticamente para a etapa correta.

## Tasks

- [x] Implementar lock e reserva atomica do caso.
- [x] Notificar cliente e prestador apos o aceite vencedor.
- [x] Liberar telefone e WhatsApp autorizado do cliente somente apos reserva valida.
- [x] Encerrar ondas pendentes e marcar targets restantes como expirados ou dispensados.
- [x] Atualizar agenda, Kanban e historico operacional com o prestador reservado.

## Entrega implementada

- O aceite vencedor do prestador continua usando `TryReserveJourneyDispatchTarget`, que reserva o caso com lock pessimista, move o card para `Prestador conectado` e dispensa os alvos restantes.
- A confirmacao publica do prestador passou a ser assincrona e, apos a reserva, aciona o `JourneyProviderConnectionService`.
- O `JourneyProviderConnectionService` libera os dados do cliente somente para o prestador vencedor, atualiza o evento no Google Calendar com os contatos das partes e registra historico operacional dedicado.
- O cliente agora recebe a conexao do prestador por Telegram quando houver `TelegramChatId` ativo; sem Telegram, o fallback operacional e e-mail.
- O prestador vencedor recebe e-mail de confirmacao com os dados liberados do cliente, janela agendada e endereco validado.
- A pagina publica `/prestadores/oportunidades/responder` passou a exibir telefone/e-mail do cliente apenas quando o prestador logico do token tambem e o prestador reservado.
