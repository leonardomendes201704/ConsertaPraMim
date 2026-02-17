# Runbook - Geracao de APKs (Cliente + Prestador)

## Objetivo
Gerar os APKs Android dos dois apps (`cliente` e `prestador`) em um unico comando.

## Script oficial
`scripts/build_apks.py`

## Pre-requisitos
- Python 3 instalado.
- Node.js/NPM instalado.
- Android SDK instalado (com `platform-tools` e `build-tools`).
- Dependencias NPM instaladas nos apps.

## Comando padrao
Executar no root do repositorio:

```bash
python scripts/build_apks.py
```

Atalho no CMD:

```bat
.\build_apks.bat
```

## Comando com URL explicita da API

```bash
python scripts/build_apks.py --api-base-url http://192.168.0.196:5193
```

## O que o script faz
1. Detecta IP local (quando `--api-base-url` nao e informado).
2. Escreve `.env.android` nos dois apps com `VITE_API_BASE_URL`.
3. Ajusta `android/local.properties` para o SDK local.
4. Executa `npm run android:apk:debug` em:
   - `conserta-pra-mim app`
   - `conserta-pra-mim-provider app`
5. Copia os APKs para `apk-output/`.
6. Gera APKs `compat` (assinados) para instalacao direta no Android.
7. Gera `apk-output/SHA256.txt` com os hashes dos APKs.

## Saida esperada
Pasta `apk-output/`:
- `ConsertaPraMim-Cliente-debug.apk`
- `ConsertaPraMim-Cliente-compat.apk`
- `ConsertaPraMim-Prestador-debug.apk`
- `ConsertaPraMim-Prestador-compat.apk`
- `SHA256.txt`

## Solucao rapida de falhas comuns
- SDK nao encontrado:
  - Informe `--sdk-root "C:\\caminho\\do\\AndroidSdk"`.
- Falha de permissao em SDK dentro de `Program Files`:
  - Use SDK em pasta gravavel do usuario (ex.: `C:\\Users\\<user>\\AndroidSdk`).
- APK nao instala no celular:
  - Tente os arquivos `*-compat.apk`.
  - Instale pelo app de arquivos (nao pelo preview do navegador).
