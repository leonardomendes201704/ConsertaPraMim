# Checklist de Monitoramento Pos-Deploy - ST-026 (Suporte)

## Janela de observacao

- Primeiros 15 minutos apos deploy
- 1 hora apos deploy
- 24 horas apos deploy

## Indicadores obrigatorios

- [ ] `Top Erros` sem crescimento anormal para rotas de suporte.
- [ ] Taxa de erro 5xx em suporte abaixo de 1%.
- [ ] Latencia p95 das rotas de suporte dentro do baseline da release anterior.
- [ ] Fila admin com atualizacao de status/mensagem em tempo esperado.
- [ ] Eventos de auditoria gravando para `support_ticket_assignment_changed`, `support_ticket_status_changed`, `support_ticket_closed`, `support_ticket_reopened`.
- [ ] Notificacoes realtime entregues (admin e prestador) ou fallback de polling funcionando.

## Endpoints de referencia

- `GET /api/admin/support-tickets`
- `GET /api/admin/support-tickets/{id}`
- `POST /api/admin/support-tickets/{id}/messages`
- `PATCH /api/admin/support-tickets/{id}/status`
- `PATCH /api/admin/support-tickets/{id}/assign`
- `POST /api/mobile/provider/support-tickets`
- `POST /api/mobile/provider/support-tickets/{id}/messages`
- `POST /api/mobile/provider/support-tickets/{id}/close`

## Acoes em caso de incidente

1. Confirmar se o problema e de API, portal admin ou portal prestador.
2. Isolar erro por correlationId e ticketId.
3. Avaliar rollback conforme `DEPLOY_ROLLBACK_ST-026_SUPORTE.md`.
4. Registrar RCA preliminar e plano de mitigacao.
