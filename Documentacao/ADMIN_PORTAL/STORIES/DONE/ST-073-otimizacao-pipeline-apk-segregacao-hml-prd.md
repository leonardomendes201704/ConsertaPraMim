# ST-073 - Otimizacao do pipeline de APK com segregacao HML/PRD

## Como
DevOps / engenharia de plataforma

## Eu quero
reduzir o tempo total de build/publicacao dos APKs e separar os artefatos por ambiente no fileserver

## Para
evitar sobrescrita entre homologacao e producao, melhorar rastreabilidade e acelerar o ciclo de entrega mobile.

## Criterios de aceite

1. Os jobs de build de APK (`client`, `provider`, `admin`) executam em paralelo apos os healthchecks de deploy, sem dependencia sequencial entre apps.
2. Os jobs de upload de APK nao dependem de upload de outro app e processam cada artefato de forma independente.
3. O workflow usa cache Gradle nos builds de APK para reduzir tempo medio de execucao entre runs.
4. O fileserver publica APKs por ambiente:
   - `dev-local` -> `/files/apks/hml/`
   - `main/master` -> `/files/apks/prd/`
5. O push de release e o resumo final exibem a URL do fileserver compativel com o ambiente corrente.
6. O manual operacional de deploy VPS documenta explicitamente a segregacao de artefatos APK por ambiente.

## Tasks

- [x] remover encadeamento sequencial entre builds de APK (client -> provider -> admin);
- [x] remover encadeamento sequencial entre uploads de APK por app;
- [x] habilitar cache Gradle nos 3 jobs de build de APK;
- [x] parametrizar diretorio de publicacao de APK por ambiente (`apks/hml` e `apks/prd`);
- [x] atualizar links de resumo/push para refletirem o diretorio de ambiente;
- [x] atualizar `Backend/DEPLOY_VPS.md` com fluxo de segregacao HML/PRD.
