# ST-002 - Bootstrap do projeto ConsertaPraMim.Web.Admin

Status: Done  
Epic: EPIC-001

## Objetivo

Criar o portal web administrativo dedicado, desacoplado do portal do prestador.

## Criterios de aceite

- Projeto `ConsertaPraMim.Web.Admin` criado e adicionado na solution.
- Login de Admin funcionando com cookie auth.
- Layout base admin funcional em desktop e mobile.
- Navegacao inicial com modulos principais disponivel.

## Tasks

- [x] Criar projeto ASP.NET MVC `ConsertaPraMim.Web.Admin` no `Backend/src`.
- [x] Adicionar referencias de `Application` e `Infrastructure` no novo projeto.
- [x] Configurar autenticacao por cookie e redirecionamento de login/forbidden.
- [x] Criar `AccountController` para login/logout admin.
- [x] Criar layout admin com menu para Dashboard, Usuarios, Pedidos, Propostas, Chats e Configuracoes.
- [x] Configurar roteamento default para `AdminHome/Index`.
- [x] Atualizar `src.sln` e `ConsertaPraMim.sln` com o novo projeto.
