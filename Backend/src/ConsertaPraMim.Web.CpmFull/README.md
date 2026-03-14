# ConsertaPraMim - CPM Full

Aplicacao web em **ASP.NET Core 9 (MVC)** para conectar clientes a profissionais de servicos residenciais.

## Visao geral

O projeto foi estruturado para funcionar como plataforma unica (web + back-end no mesmo app), sem React.  
Toda a camada de apresentacao usa Razor Views e Bootstrap, enquanto a logica de negocio fica em controllers/viewmodels/services.

## Principais funcionalidades

- Home com busca rapida por servico e CEP.
- Cards de servicos mais buscados com redirecionamento para solicitacao.
- Formulario de solicitacao de servico com validacao server-side.
- Formulario de cadastro profissional com mascaras e validacoes.
- Lista de profissionais com busca por nome/profissao/servico.
- Galeria de servicos em modal Bootstrap (thumbs + navegacao).
- Pagina de sucesso apos cadastro profissional.
- Central de suporte com formulario e FAQ abrangente.
- Paginas institucionais de Termos de uso e Privacidade.

## CEP e geolocalizacao

- Mascara de CEP nos campos relevantes.
- Integracao de consulta de CEP com APIs publicas:
  - ViaCEP (principal)
  - BrasilAPI (fallback)
- Se nao for possivel resolver endereco completo (rua/bairro/cidade/UF), o CEP digitado continua valido.
- Na Home, botao de localizacao para tentar obter CEP por geolocalizacao do navegador.

## SEO tecnico implementado

- Meta tags dinamicas por pagina (`title`, `description`, `keywords`, `robots`).
- Canonical e `hreflang`.
- Open Graph e Twitter Card.
- Dados estruturados (`JSON-LD`) globais e por pagina:
  - `LocalBusiness`, `WebSite`, `SearchAction`
  - `WebPage`, `CollectionPage`, `ItemList`, `HowTo`, `FAQPage`
- `robots.txt` dinamico.
- `sitemap.xml` dinamico.
- Paginas de sucesso/erro configuradas com `noindex`.
- Compressao de resposta (Brotli/Gzip) e cache de assets estaticos.

## Stack

- .NET 9 (`net9.0`)
- ASP.NET Core MVC
- Razor Views
- Bootstrap 5 + Bootstrap Icons
- JavaScript vanilla

## Estrutura de pastas

- `Controllers/` - rotas web e fluxo das paginas.
- `Models/` - entidades de dominio.
- `ViewModels/` - modelos especificos de tela/formulario.
- `Services/` - repositorio em memoria e regras de acesso a dados.
- `Views/` - paginas Razor.
- `wwwroot/` - CSS, JS e imagens estaticas.
- `documentacao/` - materiais operacionais e apoio da trilha.

## Documentacao operacional

- [Manual de QA e Operacao](documentacao/MANUAL_QA_OPERACAO.md)
- [Epic de integracao Chatwoot](documentacao/EPIC_CHATWOOT_FUNIS_CPM.md)

## Configuracao Chatwoot

O projeto ja possui a base inicial da integracao com `Chatwoot`, incluindo:

- binding forte da secao `Chatwoot`;
- validacao explicita quando `Chatwoot:Enabled=true`;
- cliente HTTP tipado com timeout e retentativa;
- health check interno em `/internal/health/chatwoot`.

Variaveis/parametros minimos:

- `Chatwoot__Enabled`
- `Chatwoot__BaseUrl`
- `Chatwoot__ApiAccessToken`
- `Chatwoot__AccountId`
- `Chatwoot__ClientsInboxId`
- `Chatwoot__ProvidersInboxId`
- `Chatwoot__WebhookSecret`

Exemplo local em `appsettings.Local.json`:

```json
{
  "Chatwoot": {
    "Enabled": true,
    "BaseUrl": "https://chat.seudominio.com",
    "ApiAccessToken": "seu-token",
    "AccountId": 1,
    "ClientsInboxId": 10,
    "ProvidersInboxId": 11,
    "WebhookSecret": "segredo-webhook",
    "RequestTimeoutSeconds": 15,
    "MaxRetryAttempts": 3,
    "RetryBaseDelayMs": 500
  }
}
```

## Rotas principais

- `/` - Home
- `/profissionais` - Busca e listagem de profissionais
- `/solicitar-servico` - Solicitacao de servico
- `/cadastro-profissional` - Cadastro de prestador
- `/cadastro-profissional/sucesso` - Confirmacao de cadastro
- `/suporte` - Formulario de suporte + FAQ
- `/termos-de-uso-profissionais` - Termos de uso
- `/privacidade` - Politica de privacidade
- `/robots.txt` - Regras para crawlers
- `/sitemap.xml` - Sitemap XML

## Requisitos

- .NET SDK 9.0+

## Como executar localmente

1. Restaurar pacotes:

```bash
dotnet restore
```

2. Executar em modo desenvolvimento:

```bash
dotnet run
```

3. Acessar no navegador a URL exibida no terminal.

## Build para validacao

```bash
dotnet build -p:UseAppHost=false -o .\\bin\\validation
```

## Publicacao

```bash
dotnet publish -c Release -o .\\publish
```

## Observacoes importantes

- O repositorio usa armazenamento em memoria (`InMemoryMarketplaceRepository`), sem banco de dados persistente.
- Ao reiniciar a aplicacao, dados enviados pelos formularios sao resetados.
- Integracoes externas (CEP/geolocalizacao) dependem da disponibilidade das APIs publicas e permissao do navegador.

## Roadmap sugerido

- Persistencia em banco de dados (SQL Server/PostgreSQL).
- Autenticacao e autorizacao de usuarios/profissionais.
- Painel administrativo para moderacao e acompanhamento.
- Observabilidade (logs estruturados, metricas, tracing).
- Integracao com analytics e Search Console em producao.

## Licenca

Uso interno/proprietario da plataforma ConsertaPraMim, salvo definicao formal posterior.
