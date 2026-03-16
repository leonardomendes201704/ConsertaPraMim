# ST-108 - Reserva do caso e conexao direta entre cliente e prestador

Status: Backlog
Epic: EPIC-JORNADA-001

## Objetivo

Fazer com que o primeiro prestador valido que aceitar reserve o caso e receba dados suficientes para entrar em contato diretamente com o cliente.

## Criterios de aceite

- O primeiro aceite valido reserva o caso.
- Os demais prestadores deixam de poder aceitar.
- Cliente e prestador recebem os dados necessarios para contato direto.
- O card do Kanban avanca automaticamente para a etapa correta.

## Tasks

- [ ] Implementar lock e reserva atomica do caso.
- [ ] Notificar cliente e prestador apos o aceite vencedor.
- [ ] Liberar telefone e WhatsApp autorizado do cliente somente apos reserva valida.
- [ ] Encerrar ondas pendentes e marcar targets restantes como expirados ou dispensados.
- [ ] Atualizar agenda, Kanban e historico operacional com o prestador reservado.
