# ST-089 - Publicacao do TelegramBridge na VPS com webhook HTTPS

## Como
time de operacao, backend e devops do ecossistema ConsertaPraMim

## Eu quero
publicar o `ConsertaPraMim.Web.TelegramBridge` como servico proprio da pipeline VPS, com URL publica HTTPS e healthcheck dedicado

## Para
operar o modo `Webhook` do Telegram em ambiente publicado, sem depender apenas de `LongPolling` nem de start manual fora do workflow de deploy.

## Criterios de aceite

1. O workflow `deploy-vps` detecta mudancas do bridge e publica `web-telegrambridge`.
2. Existe `Dockerfile` e compose dedicados para o bridge na VPS.
3. O healthcheck da pipeline valida `GET /health`.
4. A documentacao cobre `PUBLIC_TELEGRAM_BRIDGE_URL`, `TELEGRAM_BRIDGE_*`, `TELEGRAM_AUTOMATION_*` e o subdominio HTTPS recomendado.

## Tasks

- [x] adicionar `Dockerfile.web.telegrambridge` e `docker-compose.vps.web-telegrambridge.yml`;
- [x] publicar `deploy-web-telegrambridge` e `health-web-telegrambridge` no workflow `deploy-vps`;
- [x] ajustar `vps-deploy.sh` e `vps-deploy-service.sh` para o novo servico;
- [x] adicionar `GET /health` e `ForwardedHeaders` no runtime do bridge publicado;
- [x] atualizar `DEPLOY_VPS.md`, manual QA/Operacao, README, epic, indice e changelog com a publicacao do bridge.

## Observacao pos-release

- Em `2026-03-14`, a story recebeu hotfix operacional para alinhar `Backend/docker/vps/Dockerfile.web.telegrambridge` ao `TargetFramework` real do projeto (`net8.0`), trocando a imagem final `aspnet:9.0` por `aspnet:8.0` e eliminando o restart loop do container observado na homologacao.
