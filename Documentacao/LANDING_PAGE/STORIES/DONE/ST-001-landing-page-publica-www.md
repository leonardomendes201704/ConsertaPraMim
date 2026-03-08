# ST-001 - Landing page publica em `www.consertapramim.com`

Status: DONE

## Objetivo

Criar uma landing page publica no dominio principal `https://www.consertapramim.com`, mantendo os portais operacionais em subdominios dedicados.

## Escopo entregue

- projeto `ConsertaPraMim.Web.Landing` adicionado na solution;
- home institucional publica com CTA para cliente, prestador, admin e Swagger;
- `healthcheck` em `/health`, `robots.txt` e `sitemap.xml`;
- Dockerfile, compose e scripts de deploy para `web-landing`;
- workflow GitHub Actions com deploy seletivo e healthcheck da landing;
- template de Nginx com `www` e redirect do dominio raiz;
- documentacao operacional e QA da trilha.

## Tasks concluidas

- [x] criar projeto web dedicado para a landing;
- [x] desenhar a home publica e responsiva;
- [x] integrar `web-landing` ao deploy da VPS;
- [x] atualizar Nginx/Certbot/DNS no runbook;
- [x] publicar manual QA/Operacao e changelog.

## Validacao esperada

- `https://consertapramim.com` responde com redirect para `https://www.consertapramim.com`;
- `https://www.consertapramim.com` abre sem erro de CSP;
- CTA para `cliente`, `prestador`, `admin` e `swagger` apontam para os dominios HTTPS;
- `https://www.consertapramim.com/health` retorna `200 Healthy`.
