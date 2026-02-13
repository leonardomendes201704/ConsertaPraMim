# Relatorio de Auditoria de Seguranca - ConsertaPraMim

Data: 2026-02-13  
Escopo: `Backend/src`, `Backend/tests`, configuracoes `appsettings*`, fluxos de chat/notificacao/upload e autenticacao/autorizacao.

## Resumo executivo

Foi realizada uma varredura estatica de seguranca no projeto. O sistema tem mecanismos positivos (hash de senha com BCrypt, uso de `[Authorize]` em varias rotas, HSTS nos portais web), mas possui riscos graves para sigilo/privacidade de conversas e dados pessoais.

Principais riscos identificados:

- Superficie critica de impersonacao e vazamento no chat/notificacoes por falta de autenticacao nos hubs e por confiar em `userId/role` vindos do cliente.
- Endpoint publico de notificacao permitindo envio arbitrario de mensagens para qualquer destinatario.
- Secrets sensiveis commitados (senha de banco e chave JWT), incluindo fallback de chave JWT fixa no codigo.
- Falhas de autorizacao por objeto (IDOR) em consulta de pedidos/propostas.
- Upload de foto de perfil sem validacao de tipo/tamanho.

## Status das correcoes (2026-02-13)

- `F-01` Mitigado: hubs/chat upload agora exigem autenticacao e identidade derivada de claim (sem confiar em `userId/role` do client).
- `F-02` Mitigado: endpoint de notificacao protegido por chave interna (`X-Internal-Api-Key`) e envio ajustado no service caller.
- `F-04` Mitigado: leitura de `ServiceRequest` e `Proposal` por ID agora aplica autorizacao por recurso (cliente dono, prestador autorizado, admin).
- `F-05` Mitigado: seed agora respeita `Seed:Enabled` e `Seed:Reset` com bloqueio fora de `Development`, e senha seed forte configuravel (`Seed:DefaultPassword`).
- `F-06` Mitigado: upload de foto de perfil com validacao de extensao, MIME, tamanho maximo e assinatura de arquivo.
- `F-07` Mitigado: CORS restrito por origens explicitas e validacao JWT endurecida (issuer/audience configuraveis, HTTPS metadata fora de dev).
- `F-08` Mitigado: antiforgery global habilitado nos 3 portais MVC, logout migrado para `POST` com token e suporte de header CSRF para chamadas AJAX.
- `F-09` Mitigado: dependencia `System.IdentityModel.Tokens.Jwt` atualizada para versao corrigida.
- `F-10` Mitigado parcialmente: validacao de `actionUrl` no toast e `fileUrl` de anexos no backend/frontend para bloquear URL maliciosa.
- `F-11` Mitigado: politica de senha reforcada (minimo 8 + maiuscula + minuscula + numero + especial).

Pendencias relevantes:
- `F-03` (rotacao/retirada de secrets versionados) ainda requer acao operacional e de configuracao.
- `F-10` ainda pode receber hardening adicional (allowlist de dominos para navegacao externa e CSP dedicada).

## Metodologia aplicada

- Revisao estatica manual de controllers, services, hubs e configuracoes.
- Busca por padroes inseguros (`rg`) em autenticacao, autorizacao, upload, SQL raw, CORS, SignalR e cookies.
- Auditoria de dependencias com:
  - `dotnet list Backend/src/src.sln package --vulnerable --include-transitive`

## Matriz de achados

| ID | Severidade | Titulo |
|---|---|---|
| F-01 | Critica | Chat/SignalR permite impersonacao por confiar em identidade enviada pelo cliente |
| F-02 | Critica | Endpoint de notificacoes publico permite spoofing e abuso |
| F-03 | Alta | Secrets sensiveis hardcoded/versionados e fallback JWT inseguro |
| F-04 | Alta | IDOR em leitura de pedidos/propostas (quebra de isolamento entre usuarios) |
| F-05 | Alta | Seed destrutivo no startup + credenciais padrao fracas |
| F-06 | Alta | Upload de foto de perfil sem validacao de seguranca |
| F-07 | Alta | CORS permissivo com credenciais + validacao JWT relaxada |
| F-08 | Media | Ausencia de protecao CSRF nos portais MVC com cookie auth |
| F-09 | Media | Dependencia com CVE/advisory conhecida (JWT 7.0.3) |
| F-10 | Media | Potencial XSS/phishing por `actionUrl` e `fileUrl` sem hardening suficiente no front |
| F-11 | Baixa | Politica de senha minima fraca para cadastro publico |

## Achados detalhados

## F-01 - Critica - Chat/SignalR permite impersonacao por confiar em identidade enviada pelo cliente

Evidencias:

- `Backend/src/ConsertaPraMim.Infrastructure/Hubs/ChatHub.cs:16` (`JoinPersonalGroup(string userId)`).
- `Backend/src/ConsertaPraMim.Infrastructure/Hubs/ChatHub.cs:23` (`JoinRequestChat(..., string userId, string role)`).
- `Backend/src/ConsertaPraMim.Infrastructure/Hubs/ChatHub.cs:45` (`SendMessage(..., string userId, string role, ...)`).
- `Backend/src/ConsertaPraMim.Infrastructure/Hubs/NotificationHub.cs:7` (`JoinUserGroup(string userId)`).
- `Backend/src/ConsertaPraMim.API/Controllers/ChatAttachmentsController.cs:8` (controller sem `[Authorize]`).
- `Backend/src/ConsertaPraMim.API/Controllers/ChatAttachmentsController.cs:46` e `:47` (usa `SenderId`/`SenderRole` do request).
- `Backend/src/ConsertaPraMim.Web.Client/Views/Shared/_Layout.cshtml:719` e `:720` (cliente envia `senderId` e `senderRole` no form).
- `Backend/src/ConsertaPraMim.Web.Provider/Views/Shared/_Layout.cshtml:632` e `:633` (mesmo comportamento).

Impacto:

- Usuario anonimo ou autenticado pode tentar se passar por outro usuario ao informar `userId/role` arbitrarios.
- Risco de vazamento de conversas/notificacoes e envio de mensagens em nome de terceiros.
- Quebra direta de confidencialidade e integridade de chat.

Recomendacao:

- Exigir autenticacao em hubs e upload (`[Authorize]`).
- Nunca aceitar `userId/role` do cliente para autorizacao.
- Derivar identidade de `Context.User` (SignalR) e `User` (Controller).
- Usar `ClaimTypes.NameIdentifier` como fonte unica de identidade.
- Trocar grupos por IDs internos (nao email) e validar membership no servidor.

## F-02 - Critica - Endpoint de notificacoes publico permite spoofing e abuso

Evidencias:

- `Backend/src/ConsertaPraMim.API/Controllers/NotificationsController.cs:8` (controller sem `[Authorize]`).
- `Backend/src/ConsertaPraMim.API/Controllers/NotificationsController.cs:19` (`[HttpPost]` publico).
- `Backend/src/ConsertaPraMim.Infrastructure/Services/HubNotificationService.cs:21` e `:34` (envio por grupo baseado no `recipient`).

Impacto:

- Qualquer cliente externo pode disparar notificacoes para destinatarios arbitrarios.
- Vetor de spam, engenharia social e phishing interno.
- Possibilidade de DoS de notificacao em massa.

Recomendacao:

- Tornar endpoint interno/autenticado (ideal: admin/system only).
- Exigir policy dedicada (`AdminOnly` ou API key interna mTLS/rede privada).
- Adotar rate limit e auditoria forte para disparos.

## F-03 - Alta - Secrets sensiveis hardcoded/versionados e fallback JWT inseguro

Evidencias:

- `Backend/src/ConsertaPraMim.API/appsettings.json:10` (senha de banco).
- `Backend/src/ConsertaPraMim.API/appsettings.Development.json:12` (chave JWT).
- `Backend/src/ConsertaPraMim.Web.Client/appsettings.json:10` e `:13`.
- `Backend/src/ConsertaPraMim.Web.Provider/appsettings.json:10` e `:13`.
- `Backend/src/ConsertaPraMim.Web.Admin/appsettings.json:10` e `:13`.
- `Backend/src/ConsertaPraMim.Application/Services/AuthService.cs:66` (fallback fixo de `SecretKey` no codigo).

Impacto:

- Comprometimento de banco/token caso repositorio ou artefatos vazem.
- Token forging se chave conhecida for usada em ambiente mal configurado.

Recomendacao:

- Remover secrets do git e rotacionar imediatamente credenciais expostas.
- Usar Key Vault / Secret Manager / variaveis de ambiente.
- Remover fallback inseguro de `SecretKey` no codigo.

## F-04 - Alta - IDOR em leitura de pedidos/propostas

Evidencias:

- `Backend/src/ConsertaPraMim.API/Controllers/ServiceRequestsController.cs:74` e `:75` (`GET /api/ServiceRequests/{id}`).
- `Backend/src/ConsertaPraMim.Application/Services/ServiceRequestService.cs:129` e `:134` (retorna por ID sem validar dono/escopo).
- `Backend/src/ConsertaPraMim.API/Controllers/ProposalsController.cs:42` e `:43` (`GET /api/Proposals/request/{requestId}`).
- `Backend/src/ConsertaPraMim.Application/Services/ProposalService.cs:58` (lista por request sem validar ator).
- `Backend/src/ConsertaPraMim.Application/DTOs/ServiceRequestDTOs.cs:6` (DTO contem `ClientName` e `ClientPhone`).
- `Backend/src/ConsertaPraMim.Web.Client/Controllers/ServiceRequestsController.cs:109` e `:111` (comentario explicito de check faltante).

Impacto:

- Usuario autenticado pode consultar dados de pedidos/propostas que nao deveriam estar no seu escopo.
- Exposicao de PII (nome/telefone do cliente) e dados comerciais.

Recomendacao:

- Implementar autorizacao por recurso em todas as consultas por ID.
- No minimo: cliente so ve proprio pedido; prestador so ve pedidos elegiveis e/ou com relacionamento valido.
- Revisar DTOs para retorno minimo necessario por role/contexto.

## F-05 - Alta - Seed destrutivo no startup + credenciais padrao fracas

Evidencias:

- `Backend/src/ConsertaPraMim.API/Program.cs:104` chama `DbInitializer.SeedAsync(...)` sempre.
- `Backend/src/ConsertaPraMim.Infrastructure/Data/DbInitializer.cs:30` executa `ClearDatabaseAsync(context)` no startup.
- `Backend/src/ConsertaPraMim.Infrastructure/Data/DbInitializer.cs:172` executa `DELETE FROM` em todas as tabelas.
- `Backend/src/ConsertaPraMim.Infrastructure/Data/DbInitializer.cs:40`, `:58`, `:76-80`, `:94` usa senha `"123456"` para usuarios seed.

Impacto:

- Alto risco de indisponibilidade/perda de dados em ambiente indevido.
- Presenca de contas previsiveis em ambiente onde seed estiver ativo.

Recomendacao:

- Gate de seed por ambiente/flag forte (`Seed:Enabled` + whitelist de `Development`).
- Nunca limpar banco automaticamente fora de ambiente efemero de teste.
- Remover credenciais triviais e conta admin default em qualquer ambiente persistente.

## F-06 - Alta - Upload de foto de perfil sem validacao de seguranca

Evidencias:

- `Backend/src/ConsertaPraMim.Web.Provider/Controllers/ProfileController.cs:46` (`UploadProfilePicture(IFormFile file)`).
- `Backend/src/ConsertaPraMim.Web.Provider/Controllers/ProfileController.cs:55` salva arquivo sem validacao de extensao/MIME/tamanho.
- `Backend/src/ConsertaPraMim.API/Program.cs:123` serve `wwwroot` estaticamente.
- `Backend/src/ConsertaPraMim.Infrastructure/Services/LocalFileStorageService.cs:35` retorna URL publica em `/uploads/...`.

Impacto:

- Possivel upload de conteudo indevido/malicioso em area publica.
- Vetor de distribuicao de payload e abuso de armazenamento.

Recomendacao:

- Validar tipo real (magic bytes), extensao permitida e tamanho maximo.
- Rejeitar tipos ativos (ex.: html/svg scriptavel se nao estritamente tratado).
- Avaliar antivirus e armazenamento privado com URL assinada.

## F-07 - Alta - CORS permissivo com credenciais + validacao JWT relaxada

Evidencias:

- `Backend/src/ConsertaPraMim.API/Program.cs:65-68` (`AllowAnyHeader`, `AllowAnyMethod`, `SetIsOriginAllowed(_ => true)`, `AllowCredentials`).
- `Backend/src/ConsertaPraMim.API/Program.cs:82` (`RequireHttpsMetadata = false`).
- `Backend/src/ConsertaPraMim.API/Program.cs:88-89` (`ValidateIssuer = false`, `ValidateAudience = false`).

Impacto:

- Aumenta superficie para abuso cross-origin e validacoes fracas de token em producao.

Recomendacao:

- Restringir CORS para lista explicita de origens confiaveis.
- Em producao: habilitar `RequireHttpsMetadata`, validar emissor/audiencia e rotacionar keys.

## F-08 - Media - Ausencia de protecao CSRF nos portais MVC com cookie auth

Evidencias:

- `Backend/src/ConsertaPraMim.Web.Client/Program.cs:10` (`AddControllersWithViews` sem antiforgery global).
- `Backend/src/ConsertaPraMim.Web.Provider/Program.cs:11`.
- `Backend/src/ConsertaPraMim.Web.Admin/Program.cs:10`.
- Busca sem ocorrencias para `ValidateAntiForgeryToken|AutoValidateAntiforgeryToken|AddAntiforgery` no escopo web.
- Varios endpoints `POST` sensiveis sem token anti-CSRF (ex.: `AdminUsersController`, `AdminServiceRequestsController`, `ProfileController`, etc.).

Impacto:

- Acoes com cookie podem ser acionadas por terceiros sob certas condicoes de navegador/same-site.

Recomendacao:

- Habilitar antiforgery global para MVC e validar tokens nos POST/PUT/DELETE.
- Evitar logout via GET.

## F-09 - Media - Dependencia com vulnerabilidade conhecida (JWT 7.0.3)

Evidencias:

- `Backend/src/ConsertaPraMim.Application/ConsertaPraMim.Application.csproj:20` (`System.IdentityModel.Tokens.Jwt` v`7.0.3`).
- Scan:
  - `dotnet list Backend/src/src.sln package --vulnerable --include-transitive`
  - Advisory: `GHSA-59j7-ghrg-fj52` (Moderate).

Impacto:

- Exposicao a falha conhecida em cadeia de autenticacao/JWT.

Recomendacao:

- Atualizar `System.IdentityModel.Tokens.Jwt` e transitivos para versao corrigida.

## F-10 - Media - Potencial XSS/phishing por URLs injetadas em notificacoes/anexos

Evidencias:

- `Backend/src/ConsertaPraMim.Web.Client/Views/Shared/_Layout.cshtml:436` (`window.location.href = actionUrl`).
- `Backend/src/ConsertaPraMim.Web.Provider/Views/Shared/_Layout.cshtml:345` (mesmo).
- `Backend/src/ConsertaPraMim.Web.Client/Views/Shared/_Layout.cshtml:501`, `:505`, `:508` usa `attachment.fileUrl` em HTML sem sanitizacao de URL.
- `Backend/src/ConsertaPraMim.Web.Provider/Views/Shared/_Layout.cshtml:414`, `:418`, `:421` idem.
- `Backend/src/ConsertaPraMim.Application/Services/ChatService.cs:94` aceita `FileUrl` vindo do cliente e persiste.

Impacto:

- Vetor para phishing (redirecionamentos nao confiaveis) e possivel XSS baseado em URL maliciosa.

Recomendacao:

- Permitir somente protocolos `https/http` e dominos esperados para `actionUrl` e `fileUrl`.
- Sanitizar/validar URLs no backend e frontend antes de renderizar/navegar.

## F-11 - Baixa - Politica de senha minima fraca

Evidencias:

- `Backend/src/ConsertaPraMim.Application/Validators/AuthValidators.cs:13` (`MinimumLength(6)`).

Impacto:

- Facilita ataques de credencial stuffing/forca bruta em contas de baixo nivel de senha.

Recomendacao:

- Exigir senha forte (comprimento maior + complexidade minima) e considerar lockout/rate limit no login.

## Pontos positivos observados

- Uso de BCrypt para hash de senha (`AuthService`).
- Politica `AdminOnly` aplicada nas rotas administrativas da API.
- HTTPS redirection nos apps e HSTS nos portais web (fora de desenvolvimento).

## Priorizacao recomendada (ordem pratica)

1. Bloquear vetores criticos de impersonacao:
   - autenticar hubs/upload,
   - remover `userId/role` vindos do cliente,
   - proteger endpoint de notificacao.
2. Rotacionar secrets expostos e mover para cofre.
3. Corrigir IDOR em pedidos/propostas por autorizacao de recurso.
4. Desativar seed destrutivo fora de teste efemero.
5. Implantar validacao robusta de upload e endurecimento de URL.
6. Endurecer CORS/JWT e atualizar dependencia vulneravel.
7. Implantar anti-CSRF e ajustar logout para POST com token.

## Limitacoes desta auditoria

- Analise estatica (sem pentest ativo, fuzzing de runtime ou teste de infraestrutura/cloud).
- Nao houve revisao de configuracao de WAF/rede/KeyVault/segredos fora do codigo.
- Recomendado executar teste dinamico de seguranca apos as correcoes.
