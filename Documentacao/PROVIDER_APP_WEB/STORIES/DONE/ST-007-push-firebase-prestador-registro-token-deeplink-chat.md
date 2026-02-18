# ST-007 - Push Firebase no app prestador com registro de token e deep link para chat

Status: Done
Epic: EPIC-001

## Objetivo

Habilitar push notifications no app do prestador para eventos de chat e notificacoes operacionais com app fechado/background.

## Criterios de aceite

- App do prestador instala/sincroniza `@capacitor/push-notifications`.
- `google-services.json` do app prestador e reconhecido no projeto Android.
- Registro de token no login:
  - `POST /api/mobile/provider/push-devices/register`
- Desregistro no logout:
  - `POST /api/mobile/provider/push-devices/unregister`
- Push em foreground mostra toast e incrementa inbox de notificacoes do app.
- Toque na notificacao abre a conversa/contexto relacionado quando payload fornecer `requestId` e `providerId`.
- Fluxo web continua operando com email/senha sem dependencia de push nativo.

## Tasks

- [x] Criar/ajustar servico de push do app prestador (`initialize`, listeners, teardown, unregister).
- [x] Integrar push no `App.tsx` do prestador com ciclo de sessao autenticada.
- [x] Implementar mapeamento de payload para `ProviderAppNotification`.
- [x] Implementar acao de notificacao para abrir conversa correta.
- [x] Validar status HTTP no registro/desregistro de token.
- [x] Instalar plugin Capacitor push e sincronizar Android.
- [x] Configurar `google-services.json` no modulo Android do app prestador.
- [x] Atualizar documentacao e diagramas.

## Arquivos impactados

### App prestador

- `conserta-pra-mim-provider app/App.tsx`
- `conserta-pra-mim-provider app/services/pushNotifications.ts`
- `conserta-pra-mim-provider app/package.json`
- `conserta-pra-mim-provider app/package-lock.json`
- `conserta-pra-mim-provider app/android/app/capacitor.build.gradle`
- `conserta-pra-mim-provider app/android/capacitor.settings.gradle`
- `conserta-pra-mim-provider app/.gitignore`

### Backend (suporte push mobile)

- `Backend/src/ConsertaPraMim.API/Controllers/MobileProviderPushDevicesController.cs`
- `Backend/src/ConsertaPraMim.Application/Services/MobilePushDeviceService.cs`
- `Backend/src/ConsertaPraMim.Infrastructure/Services/MobilePushNotificationService.cs`
- `Backend/src/ConsertaPraMim.Infrastructure/Services/FirebasePushSender.cs`
- `Backend/src/ConsertaPraMim.Infrastructure/Hubs/ChatHub.cs`

### Diagramas

- `Documentacao/DIAGRAMAS/PROVIDER_APP_WEB/ST-007-push-firebase-registro-token-deeplink/fluxo-push-firebase-provider-app.mmd`
- `Documentacao/DIAGRAMAS/PROVIDER_APP_WEB/ST-007-push-firebase-registro-token-deeplink/sequencia-push-firebase-provider-app.mmd`
