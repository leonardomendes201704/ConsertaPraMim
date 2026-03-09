# ST-066 - Build APK e instalacao no Fire Stick

## Como
time tecnico/operacao

## Eu quero
um fluxo padrao para gerar o APK do app Fire TV e instalar em dispositivo Fire Stick

## Para
fazer rollout controlado do dashboard em TV sem depender de Android Studio manualmente.

## Criterios de aceite

1. O script `scripts/build_apks.py` aceita `--app firetv` e gera APK debug e compat assinados para distribuicao interna.
2. O app usa por padrao a API publicada em `https://api.consertapramim.com`.
3. Existe runbook com build, instalacao via `adb`, checklist de validacao e rollback.
4. O APK debug e gerado com sucesso no ambiente local de desenvolvimento.
5. A instalacao no Fire Stick fica documentada com os comandos exatos de `adb connect` e `adb install -r`.

## Tasks

- [x] integrar o app Fire TV ao script oficial de build de APKs;
- [x] alinhar URL publica padrao da API para build Android;
- [x] validar build local do APK debug do Fire TV;
- [x] criar runbook operacional de instalacao no Fire Stick;
- [x] registrar changelog e artefatos de saida esperados.
