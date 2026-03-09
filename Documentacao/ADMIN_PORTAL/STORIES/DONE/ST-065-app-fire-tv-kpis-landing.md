# ST-065 - App Fire TV para KPIs da landing

## Como
lideranca/operacao/comercial

## Eu quero
um app de TV read-only para acompanhar continuamente a performance da landing

## Para
usar uma tela grande no escritorio sem precisar navegar no Portal Admin desktop.

## Criterios de aceite

1. O app autentica com conta `Admin` existente usando a API publica autenticada.
2. A home do app renderiza oito KPIs principais, heatmap, top origens, top localidades e sessoes recentes.
3. A interface e navegavel por controle remoto/D-pad com foco visual claro e componentes grandes.
4. O app respeita `AutoRefreshSeconds` e `AllowedRangeDays` vindos da API.
5. O app exibe mensagens amigaveis para API offline, sessao expirada e dashboard desativado.
6. O app gera projeto Android/Fire TV com `LEANBACK_LAUNCHER` e banner para aparecer na home do Fire TV.

## Tasks

- [x] criar app React + Vite + Capacitor dedicado ao Fire TV;
- [x] implementar tela splash, login e dashboard;
- [x] integrar autenticacao admin e persistencia de sessao;
- [x] implementar renderizacao de 8 KPIs e paines secundarios;
- [x] ajustar manifesto Android TV / Fire TV com `LEANBACK_LAUNCHER` e banner;
- [x] adicionar branding com logo da plataforma;
- [x] documentar variaveis, uso e operacao basica do app.
