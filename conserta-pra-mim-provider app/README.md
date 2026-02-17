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
