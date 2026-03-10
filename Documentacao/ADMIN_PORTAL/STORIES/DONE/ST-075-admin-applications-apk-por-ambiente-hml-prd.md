# ST-075 - AdminApplications com links de APK por ambiente (HML/PRD)

## Como
Operacao / administracao

## Eu quero
que a tela `AdminApplications` exiba links de download de APK no diretorio correto do ambiente ativo

## Para
evitar download acidental do artefato de producao quando eu estiver em homologacao.

## Criterios de aceite

1. Em `DEPLOY_PROFILE=development`, os links de APK na tela `AdminApplications` apontam para `/files/apks/hml/...`.
2. Em `DEPLOY_PROFILE=production`, os links de APK na tela `AdminApplications` apontam para `/files/apks/prd/...`.
3. Se `Fileserver:ApkBaseUrl` vier sem sufixo de ambiente (legado), o controller normaliza automaticamente para o canal correto.
4. A tela continua resiliente mesmo quando metadados de publicacao nao estiverem disponiveis.
5. Existe teste de regressao cobrindo resolucao de URL para HML e PRD.

## Tasks

- [x] ajustar `AdminApplicationsController` para resolver canal de ambiente (`hml`/`prd`) por `DEPLOY_PROFILE` (com override opcional por configuracao);
- [x] normalizar `Fileserver:ApkBaseUrl` legado para incluir sufixo de ambiente quando aplicavel;
- [x] manter compatibilidade com host publico da requisicao (substituindo `localhost` quando necessario);
- [x] criar testes de regressao para validacao dos links de download em `development` e `production`;
- [x] atualizar runbook de deploy/operacao com validacao da tela `AdminApplications` por ambiente.
