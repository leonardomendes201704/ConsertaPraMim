<div align="center">
<img width="1200" height="475" alt="GHBanner" src="https://github.com/user-attachments/assets/0aa67016-6eaf-458a-adb2-6e31a0763ed6" />
</div>

# Run and deploy your AI Studio app

This contains everything you need to run your app locally.

View your app in AI Studio: https://ai.studio/apps/drive/1LXvzV2vttQ6PBj1Ik95w-HmuXy-ygXcb

## Run Locally

**Prerequisites:**  Node.js


1. Install dependencies:
   `npm install`
2. Configure environment variables in [.env.local](.env.local):
   - `GEMINI_API_KEY` (required for AI diagnostic and chat)
   - `VITE_API_BASE_URL` (optional, default: `http://187.77.48.150:5193`)
   - `VITE_DEFAULT_LOGIN_EMAIL` (optional, default: `cliente2@teste.com`)
   - `VITE_DEFAULT_LOGIN_PASSWORD` (optional, default: `SeedDev!2026`)
3. Start ConsertaPraMim API (for real login and "Meus Pedidos"):
   - Endpoint used by app: `POST /api/auth/login`
   - Endpoint used by app: `POST /api/auth/register`
   - Endpoint used by app: `GET /api/mobile/client/orders`
   - Endpoint used by app: `GET /api/mobile/client/orders/{orderId}`
4. Run the app:
   `npm run dev`

## Gerar APK Android (automatizado)

No root do repositorio:

```bash
python scripts/build_apks.py
```

Ou no CMD (atalho):

```bat
.\build_apks.bat
```

O script:
- compila app Cliente e app Prestador para Android;
- atualiza `.env.android` com `VITE_API_BASE_URL` fixo da VPS (`http://187.77.48.150:5193`);
- gera APKs em `apk-output/`;
- gera `SHA256.txt` com hashes dos arquivos.

Opcional (mantido por compatibilidade, mesmo valor da VPS):

```bash
python scripts/build_apks.py --api-base-url http://187.77.48.150:5193
```
