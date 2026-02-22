# ST-032 - CI de APK admin mobile e rollout

Status: In Progress
Epic: EPIC-010

## Objetivo

Incluir o app admin no pipeline automatizado de build/upload de APK para manter distribuicao centralizada no fileserver.

## Criterios de aceite

- `scripts/build_apks.py` gera APK debug e compat do app Admin.
- Workflow de deploy inclui install/build do app Admin.
- APK Admin e publicado no fileserver junto dos demais APKs.
- Summary do workflow lista links do APK Admin.

## Tasks

- [ ] Atualizar script de build de APK para incluir app Admin.
- [ ] Atualizar workflow `deploy-vps.yml` para instalar e buildar app Admin.
- [ ] Publicar links do APK Admin no summary do workflow.
- [ ] Validar fluxo completo de build/upload no CI.
