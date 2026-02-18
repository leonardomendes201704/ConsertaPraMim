# ST-013 - Push Firebase no app cliente com registro de token e acoes por notificacao

Status: Done
Epic: EPIC-011

## Objetivo

Entregar push notifications no app cliente para eventos de chat e notificacoes de negocio, incluindo fluxo de registro de token, tratamento de foreground e deep link por acao.

## Criterios de aceite

- App cliente instala e sincroniza `@capacitor/push-notifications` para Android.
- `google-services.json` do app cliente e reconhecido no projeto Android.
- App registra token no backend apos login:
  - `POST /api/mobile/client/push-devices/register`
- App desregistra token no logout:
  - `POST /api/mobile/client/push-devices/unregister`
- Em foreground:
  - push vira toast in-app;
  - item e inserido no sino/notificacoes.
- Ao tocar na notificacao do SO:
  - app processa payload;
  - abre o contexto correto (chat/detalhe) quando dados estiverem disponiveis.
- Login web com email/senha continua funcionando sem exigir push.

## Tasks

- [x] Criar/ajustar servico de push do app cliente (`initialize`, listeners, teardown, unregister).
- [x] Integrar push no ciclo de vida do `App.tsx` (sessao autenticada e logout).
- [x] Mapear payload de push para notificacao interna do app.
- [x] Tratar evento de acao da notificacao para navegar ao contexto correto.
- [x] Validar respostas HTTP no registro/desregistro de token.
- [x] Instalar plugin Capacitor push e sincronizar Android.
- [x] Configurar `google-services.json` no modulo Android do app cliente.
- [x] Gerar diagramas Mermaid de fluxo e sequencia e atualizar indices.

## Arquivos impactados

### App cliente

- `conserta-pra-mim app/App.tsx`
- `conserta-pra-mim app/services/pushNotifications.ts`
- `conserta-pra-mim app/package.json`
- `conserta-pra-mim app/package-lock.json`
- `conserta-pra-mim app/android/app/capacitor.build.gradle`
- `conserta-pra-mim app/android/capacitor.settings.gradle`
- `conserta-pra-mim app/.gitignore`

### Backend (suporte push mobile)

- `Backend/src/ConsertaPraMim.API/Controllers/MobileClientPushDevicesController.cs`
- `Backend/src/ConsertaPraMim.Application/Services/MobilePushDeviceService.cs`
- `Backend/src/ConsertaPraMim.Infrastructure/Services/MobilePushNotificationService.cs`
- `Backend/src/ConsertaPraMim.Infrastructure/Services/FirebasePushSender.cs`

### Diagramas

- `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-015-push-firebase-registro-token-acoes/fluxo-push-firebase-consumer-app.mmd`
- `Documentacao/DIAGRAMAS/CONSUMER_APP_WEB/APP-015-push-firebase-registro-token-acoes/sequencia-push-firebase-consumer-app.mmd`
