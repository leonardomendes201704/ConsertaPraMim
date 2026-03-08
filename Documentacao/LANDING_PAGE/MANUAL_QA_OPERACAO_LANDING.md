# Manual QA/Operacao - Landing Page Publica

## Escopo

Este manual cobre a landing publica `ConsertaPraMim.Web.Landing`, publicada em `https://www.consertapramim.com`, o redirect do dominio raiz `https://consertapramim.com`, a captura de leads comerciais de cliente/prestador via modal Bootstrap e os deep links `https://www.consertapramim.com/Cliente` e `https://www.consertapramim.com/Prestador`.

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
curl -I https://www.consertapramim.com/og-logo-consertapramim.png
curl -I https://www.consertapramim.com/Cliente
curl -I https://www.consertapramim.com/Prestador
curl -s https://www.consertapramim.com | grep data-lead-capture-url
curl -I https://consertapramim.com
curl -I https://api.consertapramim.com/health
```

Esperado:
- `127.0.0.1:5088/health` -> `200`
- `https://www.consertapramim.com` -> `200`
- `https://www.consertapramim.com/health` -> `200`
- `https://www.consertapramim.com/og-logo-consertapramim.png` -> `200`
- `https://www.consertapramim.com/Cliente` -> `200`
- `https://www.consertapramim.com/Prestador` -> `200`
- `data-lead-capture-url="https://api.consertapramim.com/api/landing-leads/public"` no HTML publicado
- `https://consertapramim.com` -> `301` ou `308` para `https://www.consertapramim.com`
- `https://api.consertapramim.com/health` -> `200`

## Checklist funcional

1. Abrir `https://www.consertapramim.com` em desktop.
2. Abrir `https://www.consertapramim.com` em viewport mobile.
3. Confirmar que o menu mobile abre/fecha.
4. Confirmar que o header exibe:
   - wordmark `ConsertaPraMim` em imagem unica na topbar, sem texto duplicado ao lado
   - links `Início`, `Sobre`, `Contato`
   - CTA `Entrar`
5. Confirmar no `view-source` da home:
   - `og:title`
   - `og:description`
   - `og:image`
   - `og:url`
   - `og:type`
   - `twitter:card=summary_large_image`
   - `og:image` apontando para `https://www.consertapramim.com/og-logo-consertapramim.png`
   - `rel="icon"` apontando para `https://www.consertapramim.com/og-logo-consertapramim.png`
6. Validar que `og-logo-consertapramim.png` abre publicamente e responde `200`.
7. Validar as duas cards principais da home:
   - `Para Clientes`
   - `Para Profissionais`
8. Validar a seção `Testemunhos` logo abaixo do bloco institucional:
   - existem duas colunas visíveis;
   - a coluna de clientes exibe 5 depoimentos;
   - a coluna de prestadores exibe 5 depoimentos;
   - os cards permanecem legíveis em desktop e mobile.
9. Antes de qualquer clique, validar:
   - nenhum modal de captacao aparece aberto no carregamento inicial;
   - nenhum formulario `Cliente`/`Prestador` fica visivel na home;
   - nao existe rolagem automatica para o fim da pagina.
10. Clicar em `Encontrar profissional` e validar:
   - o modal de captacao abre no centro da tela;
   - o bloco `Contato` aparece dentro do modal;
   - o formulario `Cliente` fica visivel;
   - o formulario `Prestador` permanece oculto/inativo;
   - nao existem toggles `Cliente/Prestador` visiveis acima do formulario.
11. Clicar em `Cadastrar-se como parceiro` e validar o espelho do fluxo para `Prestador`.
12. Clicar em `Contato` no header e validar:
   - abertura do modal de captacao;
   - exibicao do bloco `Contato`;
   - abertura do formulario `Cliente`.
13. Abrir `https://www.consertapramim.com/Cliente` e validar:
   - a landing carrega normalmente;
   - o modal abre automaticamente;
   - o formulario `Cliente` fica ativo.
14. Abrir `https://www.consertapramim.com/Prestador` e validar:
   - a landing carrega normalmente;
   - o modal abre automaticamente;
   - o formulario `Prestador` fica ativo.
15. Fechar o modal por:
   - botao `X`;
   - clique no backdrop;
   - tecla `ESC`.
   Esperado: modal fecha sem navegar nem recarregar a pagina.
16. Preencher e enviar um lead `Cliente` com sucesso.
    Esperado:
   - o feedback visual `Dados enviados com sucesso!` aparece;
   - o modal fecha automaticamente;
   - um aviso visual de sucesso permanece visivel por alguns segundos fora do modal.
17. Preencher e enviar um lead `Prestador` com sucesso.
    Esperado:
   - o feedback visual `Dados enviados com sucesso!` aparece;
   - o modal fecha automaticamente;
   - um aviso visual de sucesso permanece visivel por alguns segundos fora do modal.
18. Confirmar ausencia de erros de `Mixed Content`, `Content-Security-Policy` e `CORS` no console.
19. Confirmar que nao ha bloqueio de `inline script` no console ao carregar a pagina.
20. Induzir falha de rede ou API indisponivel e validar:
   - o texto tecnico `Failed to fetch` nao aparece para o usuario;
   - o formulario exibe mensagem amigavel orientando nova tentativa.
21. Confirmar presencia dos links de rodape:
   - `Cliente`
   - `Prestador`
   - `Admin`
   - `Swagger`
22. Validar `https://www.consertapramim.com/robots.txt`.
23. Validar `https://www.consertapramim.com/sitemap.xml`.

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
   - a marca usa a imagem `logo-top-bar-consertapramim.png` como wordmark unico
2. Hero centralizado com titulo `Bem-vindo ao ConsertaPraMim`.
3. Dois cards de entrada em destaque:
   - cliente com CTA que abre o formulario de lead `Cliente`
   - profissional com CTA que abre o formulario de lead `Prestador`
4. Secao `Testemunhos` logo abaixo do bloco institucional, com 5 depoimentos de clientes e 5 de prestadores.
5. O modal de captacao permanece fechado no carregamento inicial e abre apenas quando um CTA principal for acionado.
6. O bloco `Contato` e o formulario correspondente aparecem juntos dentro do modal, sem toggles intermediarios.
7. Existem deep links dedicados para abrir o modal direto:
   - `/Cliente`
   - `/Prestador`
8. O `head` da home inclui metadados `Open Graph` e `Twitter Card` apontando para `og-logo-consertapramim.png`.
9. O favicon da home usa a mesma arte `og-logo-consertapramim.png` para manter consistencia entre aba do navegador e preview social.
10. Footer enxuto com links uteis e copyright.
11. Link `Contato` do header reaproveita o mesmo fluxo de captacao do CTA de cliente.

## Troubleshooting

### O CTA ainda redireciona para portal em vez de abrir formulario/modal

Verificar se o JS da landing carregou corretamente:

```bash
curl -I https://www.consertapramim.com/js/site.js
```

Conferir no browser se o `<body>` possui o atributo `data-lead-capture-url`, se o Bootstrap local carregou e se o listener dos botoes foi registrado.

### `/Cliente` ou `/Prestador` nao abrem o modal automaticamente

Validar se o `<body>` publicado contem `data-initial-lead-origin="client"` ou `data-initial-lead-origin="provider"` nessas rotas e se o `site.js` publicado executa `openLeadCapture(...)` no carregamento quando esse atributo existir.

### O formulario retorna erro tecnico de rede para o usuario

Validar se `PUBLIC_API_URL` aponta para `https://api.consertapramim.com`, se o endpoint `POST /api/landing-leads/public` responde via browser e se o `site.js` publicado converte indisponibilidade de rede para uma mensagem amigavel. O usuario nao deve ver `Failed to fetch`.

### O modal de captacao abre carregado ou os formularios ficam visiveis na home

Conferir se o CSS publicado contem a regra:

```css
[hidden] {
    display: none !important;
}
```

Se a regra nao estiver no asset final, recrear o container `web-landing` para invalidar cache do publish.

### O bloco `Contato` nao abre em modal

Conferir se a `Index.cshtml` publicada mantem o markup `id="leadCaptureModal"` e se o layout referencia:

- `~/lib/bootstrap/dist/css/bootstrap.min.css`
- `~/lib/bootstrap/dist/js/bootstrap.bundle.min.js`

Conferir tambem se o JS publicado usa `window.bootstrap.Modal`.

### `Failed to fetch` ou erro de CORS ao enviar lead

Verificar:

```bash
curl -I https://api.consertapramim.com/health
curl -s https://www.consertapramim.com | grep data-lead-capture-url
```

Esperado no HTML publicado:

```text
data-lead-capture-url="https://api.consertapramim.com/api/landing-leads/public"
```

Se o HTML ainda renderizar `http://187.77.48.150:5193`, a landing foi publicada com configuracao legada. Na VPS:

```bash
cd ~/ConsertaPraMimWeb
git pull origin main
MSSQL_CONTAINER_NAME=mssql-mssql-1 MSSQL_HOST_ALIAS=mssql ./scripts/deploy/vps-deploy-service.sh "$PWD" web-landing
```

Na API, manter `PUBLIC_LANDING_URL=https://www.consertapramim.com` e `PUBLIC_API_URL=https://api.consertapramim.com`.

### Erro de CSP ao enviar lead

Conferir o header `Content-Security-Policy` da landing e validar se:

- `connect-src` inclui `https://api.consertapramim.com`
- nao existe mais `<script>` inline para `window.landingConfig` no HTML publicado

### Card do WhatsApp/Telegram/LinkedIn nao mostra a imagem

Verificar:

```bash
curl -I https://www.consertapramim.com/og-image.jpg
curl -I https://www.consertapramim.com/og-logo-consertapramim.png
```

Esperado:

- `200`
- imagem publica
- dimensoes minimas acima de `300x200`
- tamanho abaixo de `5MB`

Se a URL da imagem no `head` estiver incorreta, revisar `ViewData["OpenGraphImage"]` no `HomeController`.

### Lead nao aparece no banco

Verificar logs da API:

```bash
docker logs --tail 200 cpm-api
```

Confirmar se a migration da tabela de leads foi aplicada e se a resposta do endpoint retornou `200`.

### Cards/imagens nao carregam

Verificar se os assets locais existem no publish do container:

```bash
docker exec cpm-web-landing ls -la /app/wwwroot
docker exec cpm-web-landing ls -la /app/wwwroot/images
curl -I https://www.consertapramim.com/images/landing-client-card.png
curl -I https://www.consertapramim.com/images/landing-provider-card.png
curl -I https://www.consertapramim.com/images/logo-top-bar-consertapramim.png
curl -I https://www.consertapramim.com/og-image.jpg
curl -I https://www.consertapramim.com/og-logo-consertapramim.png
```

Esperado:
- os arquivos `landing-client-card.png`, `landing-provider-card.png`, `logo-top-bar-consertapramim.png`, `og-image.jpg` e `og-logo-consertapramim.png` existem no publish
- todos respondem `200`
