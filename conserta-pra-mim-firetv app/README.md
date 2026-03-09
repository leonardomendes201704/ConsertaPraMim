# ConsertaPraMim TV

App Android TV / Fire TV focado em leitura operacional da landing publica.

## O que exibe

- 8 KPIs principais da landing
- heatmap agregado fase 1
- top origens
- top localidades
- sessoes recentes
- refresh automatico conforme configuracao runtime da API

## Scripts

- `npm run dev`
- `npm run build`
- `npm run android:add`
- `npm run android:sync`
- `npm run android:apk:debug`

## Variaveis de ambiente

Crie `.env.android` com:

```env
VITE_API_BASE_URL=https://api.consertapramim.com
```

O app autentica com conta `Admin` existente e consome `GET /api/admin/fire-tv/landing-dashboard`.

## Build padrao do repositorio

Para gerar o APK via script oficial:

```bash
python scripts/build_apks.py --app firetv
```

Saida esperada:

- `apk-output/ConsertaPraMim-FireTV-debug.apk`
- `apk-output/ConsertaPraMim-FireTV-compat.apk`

## Instalacao no Fire TV

1. Habilite `ADB Debugging` no dispositivo.
2. Descubra o IP do Fire TV.
3. Rode `adb connect <IP>:5555`.
4. Instale o APK com `adb install -r apk-output/ConsertaPraMim-FireTV-debug.apk`.
