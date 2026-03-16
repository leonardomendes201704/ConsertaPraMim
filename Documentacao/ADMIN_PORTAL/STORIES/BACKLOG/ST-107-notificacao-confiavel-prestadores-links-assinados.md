# ST-107 - Notificacao confiavel para prestadores com links assinados

Status: Backlog
Epic: EPIC-JORNADA-001

## Objetivo

Garantir que a oportunidade chegue ao prestador mesmo quando ele usa bot em outros canais, capturando o aceite por mecanismo controlado.

## Criterios de aceite

- Toda oportunidade gera email com CTA assinado.
- A resposta do prestador nao depende de parsing de texto.
- Existe rastreio de envio, abertura, clique e aceite.
- O aceite e a recusa oficiais acontecem via link assinado ou portal/app.

## Tasks

- [ ] Criar templates de email com `Aceitar` e `Recusar`.
- [ ] Gerar links assinados, expiraveis e idempotentes.
- [ ] Criar endpoint de aceite e recusa autenticado por token assinado.
- [ ] Registrar telemetria de envio, abertura e clique.
- [ ] Definir canal complementar opcional sem depender dele para aceite.
- [ ] Cobrir link expirado, clique repetido e target ja reservado.
