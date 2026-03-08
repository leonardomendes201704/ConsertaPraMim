# Manual QA/Operacao - Landing Page Publica

## Escopo

Este manual cobre a landing publica `ConsertaPraMim.Web.Landing`, publicada em `https://www.consertapramim.com`, o redirect do dominio raiz `https://consertapramim.com` e a captura de leads comerciais de cliente/prestador ao final da pagina.

## Componentes envolvidos

- projeto: `Backend/src/ConsertaPraMim.Web.Landing`
- API: `Backend/src/ConsertaPraMim.API`
- compose: `Backend/docker-compose.vps.web-landing.yml`, `Backend/docker-compose.vps.yml`, `Backend/docker-compose.vps.api.yml`
- dockerfile: `Backend/docker/vps/Dockerfile.web.landing`
- proxy: `Backend/docker/vps/nginx.portals.https.conf.example`
- deploy: `scripts/deploy/vps-deploy.sh`, `scripts/deploy/vps-deploy-service.sh`
- workflow: `.github/workflows/deploy-vps.yml`

## Configuracao minima

No `Backend/.env.vps`:

```env
PUBLIC_LANDING_URL=https://www.consertapramim.com
LANDING_PORT=5088
PUBLIC_CLIENT_URL=https://cliente.consertapramim.com
PUBLIC_PROVIDER_URL=https://prestador.consertapramim.com
PUBLIC_ADMIN_URL=https://admin.consertapramim.com
PUBLIC_API_URL=https://api.consertapramim.com
```

## Checklist de deploy

1. DNS `consertapramim.com` e `www.consertapramim.com` apontando para a VPS.
2. Template Nginx com `__ROOT_DOMAIN__` e `__WWW_DOMAIN__` substituidos.
3. Certificado emitido para raiz e `www`.
4. `PUBLIC_LANDING_URL` presente no CORS da API em producao.
5. Deploy do container:

```bash
cd ~/ConsertaPraMimWeb
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql scripts/deploy/vps-deploy-service.sh "$PWD" web-landing
```

## Smoke test tecnico

Validar na VPS:

```bash
curl -I http://127.0.0.1:5088/health
curl -I https://www.consertapramim.com
curl -I https://www.consertapramim.com/health
curl -I https://consertapramim.com
curl -I https://api.consertapramim.com/health
```

Esperado:
- `127.0.0.1:5088/health` -> `200`
- `https://www.consertapramim.com` -> `200`
- `https://www.consertapramim.com/health` -> `200`
- `https://consertapramim.com` -> `301` ou `308` para `https://www.consertapramim.com`
- `https://api.consertapramim.com/health` -> `200`

## Checklist funcional

1. Abrir `https://www.consertapramim.com` em desktop.
2. Abrir `https://www.consertapramim.com` em viewport mobile.
3. Confirmar que o menu mobile abre/fecha.
4. Confirmar que o header exibe:
   - marca `ConsertaPraMim`
   - links `Início`, `Sobre`, `Contato`
   - CTA `Entrar`
5. Validar as duas cards principais da home:
   - `Para Clientes`
   - `Para Profissionais`
6. Antes de qualquer clique, validar:
   - a secao `#captacao` nao exibe o bloco `Contato`;
   - o container do formulario permanece oculto;
   - nenhum formulario `Cliente`/`Prestador` fica visivel no carregamento inicial.
7. Clicar em `Encontrar profissional` e validar:
   - a pagina rola ate a secao de captacao;
   - o bloco `Contato` passa a ficar visivel;
   - o formulario `Cliente` fica visivel;
   - o formulario `Prestador` permanece oculto/inativo;
   - nao existem toggles `Cliente/Prestador` visiveis acima do formulario.
8. Clicar em `Cadastrar-se como parceiro` e validar o espelho do fluxo para `Prestador`.
9. Clicar em `Contato` no header e validar:
   - rolagem ate `#captacao`;
   - exibicao do bloco `Contato`;
   - abertura do formulario `Cliente`.
10. Preencher e enviar um lead `Cliente` com sucesso.
11. Preencher e enviar um lead `Prestador` com sucesso.
12. Confirmar ausencia de erros de `Mixed Content`, `Content-Security-Policy` e `CORS` no console.
13. Confirmar que nao ha bloqueio de `inline script` no console ao carregar a pagina.
14. Confirmar presencia dos links de rodape:
   - `Cliente`
   - `Prestador`
   - `Admin`
   - `Swagger`
15. Validar `https://www.consertapramim.com/robots.txt`.
16. Validar `https://www.consertapramim.com/sitemap.xml`.

## Dados esperados por lead

### Cliente

- origem `Client`
- nome
- telefone
- email
- cidade
- UF
- bairro
- categoria/servico desejado
- descricao do problema/interesse

### Prestador

- origem `Provider`
- nome do responsavel
- telefone
- email
- cidade base
- UF
- bairro/regiao
- especialidade principal
- empresa/nome fantasia (opcional)
- documento (opcional)
- anos de experiencia (opcional)
- observacoes

### Metadados tecnicos persistidos

- IP
- `X-Forwarded-For`
- `User-Agent`
- `Accept-Language`
- `Referer`
- URL atual da pagina
- host, scheme e path
- query string/UTM
- idioma do browser
- resolucao de tela
- plataforma do dispositivo
- time zone
- `CreatedAt` em UTC

## Layout esperado da home

1. Header claro com navegacao simples e CTA destacado a direita.
2. Hero centralizado com titulo `Bem-vindo ao ConsertaPraMim`.
3. Dois cards de entrada em destaque:
   - cliente com CTA que abre o formulario de lead `Cliente`
   - profissional com CTA que abre o formulario de lead `Prestador`
4. Secao final de captacao inicialmente oculta, exibida apenas quando um CTA principal for acionado.
5. O bloco `Contato` e o formulario correspondente aparecem juntos, sem toggles intermediarios.
6. Footer enxuto com links uteis e copyright.
7. Link `Contato` do header reaproveita o mesmo fluxo de captacao do CTA de cliente.

## Troubleshooting

### O CTA ainda redireciona para portal em vez de abrir formulario

Verificar se o JS da landing carregou corretamente:

```bash
curl -I https://www.consertapramim.com/js/site.js
```

Conferir no browser se o `<body>` possui o atributo `data-lead-capture-url` e se o listener dos botoes foi registrado.

### Os dois formularios aparecem abertos no carregamento inicial

Conferir se o CSS publicado contem a regra:

```css
[hidden] {
    display: none !important;
}
```

Se a regra nao estiver no asset final, recrear o container `web-landing` para invalidar cache do publish.

### O bloco `Contato` aparece carregado sem clique

Conferir se a `Index.cshtml` publicada mantem o cabeçalho `Contato` dentro do container `data-lead-shell hidden` e se o JS remove `hidden` apenas no clique de CTA.

### `Failed to fetch` ou erro de CORS ao enviar lead

Verificar:

```bash
curl -I https://api.consertapramim.com/health
```

Na VPS, confirmar `PUBLIC_LANDING_URL` no compose da API e recrear o servico se necessario.

### Erro de CSP ao enviar lead

Conferir o header `Content-Security-Policy` da landing e validar se:

- `connect-src` inclui `https://api.consertapramim.com`
- nao existe mais `<script>` inline para `window.landingConfig` no HTML publicado

### Lead nao aparece no banco

Verificar logs da API:

```bash
docker logs --tail 200 cpm-api
```

Confirmar se a migration da tabela de leads foi aplicada e se a resposta do endpoint retornou `200`.

### Cards/imagens nao carregam

Verificar se os assets locais existem no publish do container:

```bash
docker exec cpm-web-landing ls -la /app/wwwroot/images
curl -I https://www.consertapramim.com/images/landing-client-card.png
curl -I https://www.consertapramim.com/images/landing-provider-card.png
```

Esperado:
- os arquivos `landing-client-card.png` e `landing-provider-card.png` existem em `/app/wwwroot/images`
- ambos respondem `200`
