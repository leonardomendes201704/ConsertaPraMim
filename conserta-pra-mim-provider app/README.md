# Conserta Pra Mim - Provider App

App web/mobile-first para operacao do prestador.

## Run

```bash
npm install
npm run dev
```

## Variaveis

- `VITE_API_BASE_URL` (default: `https://localhost:7281`)

## Endpoints consumidos

- `GET /api/mobile/provider/dashboard`
- `GET /api/mobile/provider/requests`
- `GET /api/mobile/provider/requests/{requestId}`
- `POST /api/mobile/provider/requests/{requestId}/proposals`
- `GET /api/mobile/provider/proposals`

## Gerar APK Android (automatizado)

No root do repositorio:

```bash
python scripts/build_apks.py
```

Ou no CMD:

```bat
.\build_apks.bat
```

Se precisar usar uma URL especifica da API:

```bash
python scripts/build_apks.py --api-base-url http://192.168.0.196:5193
```

Saida dos APKs: `apk-output/`
