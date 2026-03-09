# Runbook ST-066 - Build e instalacao do dashboard Fire TV

## Objetivo

Padronizar a geracao do APK do app `ConsertaPraMim TV`, a instalacao no Fire Stick / Fire TV e a validacao operacional inicial.

## Pre-requisitos

- JDK instalado e `JAVA_HOME` configurado.
- Android SDK com `platform-tools` e `build-tools` disponiveis.
- Python 3 para o script `scripts/build_apks.py`.
- Fire Stick / Fire TV na mesma rede do computador.
- Conta `Admin` valida para autenticar no app.

## Build padrao

1. Na raiz do repositorio, execute:

```bash
python scripts/build_apks.py --app firetv
```

2. Artefatos esperados em `apk-output/`:
- `ConsertaPraMim-FireTV-debug.apk`
- `ConsertaPraMim-FireTV-compat.apk`
- `SHA256.txt`

## Instalacao no Fire Stick

1. No dispositivo, habilite:
- `Developer options`
- `ADB Debugging`
- `Apps from Unknown Sources` (quando aplicavel)

2. Descubra o IP do dispositivo.

3. No computador:

```bash
adb connect <IP_DO_FIRETV>:5555
adb install -r apk-output/ConsertaPraMim-FireTV-debug.apk
```

4. Abra o app `ConsertaPraMim TV` na home do Fire TV.

## Checklist de validacao

- [ ] O app aparece na home do Fire TV com banner.
- [ ] A tela splash abre sem travar.
- [ ] O login com conta admin funciona.
- [ ] Os 8 KPIs carregam.
- [ ] O heatmap e as listas secundarias aparecem.
- [ ] O refresh manual funciona.
- [ ] O auto refresh atualiza a tela sem derrubar a sessao.
- [ ] O logout limpa a sessao local.

## Troubleshooting rapido

### App abre mas nao autentica
- Confirmar `https://api.consertapramim.com/health` acessivel.
- Confirmar credencial `Admin` valida.
- Verificar conectividade do Fire TV com internet.

### Dashboard vazio
- Verificar se existe trafego da landing e analytics habilitado.
- Verificar se a secao runtime `FireTvDashboard` esta `Enabled=true`.

### APK nao gera
- Confirmar `JAVA_HOME` e Android SDK.
- Confirmar `build-tools` instalados.
- Reexecutar `python scripts/build_apks.py --app firetv`.

## Rollback

1. Reinstalar APK anterior conhecido:

```bash
adb install -r <apk-anterior>.apk
```

2. Se necessario, desativar temporariamente o dashboard pela configuracao runtime `FireTvDashboard.Enabled=false` no Portal Admin.
