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

3. Branding de icone (launcher):
- Os recursos Android do launcher ficam em `conserta-pra-mim-firetv app/android/app/src/main/res/mipmap-*`.
- Quando houver troca de logo oficial, regenerar os arquivos `ic_launcher.png`, `ic_launcher_round.png` e `ic_launcher_foreground.png` antes do build do APK.

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
- [ ] Apos o login, o menu central exibe os botoes `Metricas da landing` e `Visao operacional`.
- [ ] Na visao operacional, o topo exibe latencia, relogio e badges por alvo (`API`, `Portal Admin`, `Portal Cliente`, `Portal Prestador`) com cor por status.
- [ ] Os 8 KPIs carregam com delta comparativo quando `ComparisonMode != none`.
- [ ] Os filtros `Janela`, `Origem` e `Comparacao` funcionam pelo controle remoto.
- [ ] O heatmap, o scrollmap e o ranking de elementos aparecem quando habilitados.
- [ ] A `Visao operacional` exibe status realtime, mapa operacional, grafico diario em linha e KPIs executivos sem overflow visual.
- [ ] Ao entrar na `Visao operacional` pelo menu, o alerta sonoro de entrada toca uma unica vez.
- [ ] O botao `Voltar` retorna ao menu central em ambas as views.
- [ ] O refresh manual funciona.
- [ ] O auto refresh atualiza a tela sem derrubar a sessao.
- [ ] O logout limpa a sessao local.

## Parametros runtime relevantes

Na secao `Configuracoes -> Fire TV Dashboard`, validar principalmente:

- `DefaultOriginFilter`
- `OriginFilters`
- `DefaultComparisonMode`
- `ComparisonModes`
- `ShowComparison`
- `ShowHeatmap`
- `ShowScrollmap`
- `ShowElementRanking`
- `ElementRankingSize`
- `ShowLandingView`
- `ShowOperationsView`
- `DefaultView`
- `OperationsHistoryDays`
- `OperationsRefreshSeconds`
- `SignalRPulseSeconds`
- `OperationsMapMaxProviders`
- `OperationsMapMaxRequests`
- `OperationsRecentActivitySize`
- `OperationsHealthCheckTimeoutMs`
- `HealthTargets`

## Troubleshooting rapido

### App abre mas nao autentica
- Confirmar `https://api.consertapramim.com/health` acessivel.
- Confirmar credencial `Admin` valida.
- Verificar conectividade do Fire TV com internet.

### Dashboard vazio
- Verificar se existe trafego da landing e analytics habilitado.
- Verificar se a secao runtime `FireTvDashboard` esta `Enabled=true`.

### Visao operacional sem dados ou com cards zerados
- Confirmar se o endpoint `GET /api/admin/fire-tv/operations-dashboard` responde com `200`.
- Confirmar se existe massa operacional em pedidos, agendamentos e prestadores cadastrados no ambiente.
- Verificar se `OperationsHistoryDays` nao foi configurado com janela curta demais.

### Status realtime aparece offline
- Confirmar se o hub `/fireTvDashboardHub` esta mapeado e acessivel na API.
- Confirmar se o origin do app esta permitido no CORS quando o app for testado fora do Fire Stick.
- Confirmar se `SignalRPulseSeconds` esta entre `5` e `60`.

### Som de entrada da visao operacional nao toca
- Confirmar se o arquivo `public/sounds/operational-enter.mp3` foi empacotado no build.
- Confirmar se o acesso a tela ocorreu por interacao do usuario no menu (alguns WebViews bloqueiam autoplay sem gesto).
- Fechar e reabrir o app apos reinstalar APK para limpar cache de assets.

### Botao voltar do controle nao retorna ao menu
- Confirmar se o build publicado inclui `@capacitor/app` instalado no app Fire TV.
- Confirmar se o APK foi regenerado apos alteracao em `App.tsx` (`python scripts/build_apks.py --app firetv`).
- Validar no dispositivo se o evento de back esta chegando (remoto ou tecla `Esc/Backspace` em ambiente web de teste).

### Health check mostra alvos offline indevidamente
- Revisar a lista `HealthTargets` na secao `Configuracoes -> Fire TV Dashboard`.
- Confirmar se as URLs configuradas respondem com `200` ou `302`.
- Ajustar `OperationsHealthCheckTimeoutMs` se houver alta latencia real do ambiente.

### Legenda `Prestadores | Pedidos` some apos refresh do mapa
- Confirmar se o build publicado inclui os ajustes de camada (`z-index`) do mapa e da legenda.
- Fazer rebuild/preview limpo do app (`npm run build` + `vite preview`) para evitar bundle stale.
- Validar se nao existe instancia antiga do preview na mesma porta.

### Layout escalou, mas ficou deslocado (nao centralizado) na TV
- Confirmar se o build publicado inclui a shell com `--tv-offset-x/--tv-offset-y` e stage absoluto.
- Gerar novo APK apos o ajuste: `python scripts/build_apks.py --app firetv`.
- Reinstalar com `adb install -r apk-output/ConsertaPraMim-FireTV-debug.apk`.
- Fechar/reabrir o app no Fire TV para evitar cache de instancia anterior.

### Scrollmap ou ranking nao aparecem
- Confirmar `ShowScrollmap=true` e `ShowElementRanking=true` no runtime config.
- Confirmar se ja existe telemetria de scroll/click na landing para o periodo filtrado.

### Filtro comparativo nao muda os KPIs
- Confirmar `ShowComparison=true` no runtime config.
- Confirmar `comparisonMode=previous_period` no payload do endpoint `/api/admin/fire-tv/landing-dashboard`.

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
