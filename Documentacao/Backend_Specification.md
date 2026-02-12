# Especificação Técnica do Backend - ConsertaPraMim (.NET 9)

## Visão Geral
Este documento define a arquitetura, modelo de dados e endpoints da API para o aplicativo "ConsertaPraMim". O backend será desenvolvido em .NET 9, utilizando SQLite inicialmente e estruturado para migração futura para PostgreSQL.

## Tecnologias
- **Framework**: .NET 9
- **Linguagem**: C# 13
- **ORM**: Entity Framework Core 9
- **Banco de Dados**: SQLite (Dev) -> PostgreSQL (Prod)
- **Autenticação**: JWT (JSON Web Tokens)
- **Documentação API**: Swagger (OpenAPI)
- **Logging**: Serilog

## Arquitetura
A solução seguirá os princípios da **Clean Architecture** (Arquitetura Limpa), dividida em camadas:

1.  **API**: Controllers, Middlewares, Filtros. Ponto de entrada da aplicação.
2.  **Application**: Casos de uso, DTOs, Interfaces de Serviço, Validadores.
3.  **Domain**: Entidades, Value Objects, Exceções de Domínio, Interfaces de Repositório.
4.  **Infrastructure**: Implementação de Repositórios (EF Core), Serviços Externos (Email, Storage, etc.), Migrations.

## Modelo de Dados (Entidades)

### 1. User (Base)
Tabela única para usuários, distinguindo perfis via Roles.
- `Id` (Guid)
- `Name` (String)
- `Email` (String, Unique)
- `Phone` (String, Unique)
- `PasswordHash` (String)
- `Role` (Enum: Client, Provider, Admin)
- `CreatedAt` (DateTime)
- `IsActive` (Boolean)

### 2. ProviderProfile (Extensão para Prestadores)
Relacionamento 1:1 com User (apenas para role Provider).
- `UserId` (FK)
- `PlanId` (Enum ou FK: Bronze, Silver, Gold)
- `RadiusKm` (Double)
- `Categories` (String/JSON ou Tabela NxN)
- `IsVerified` (Boolean)
- `Rating` (Double)
- `ReviewCount` (Int)

### 3. ServiceRequest (Pedido)
O coração do sistema.
- `Id` (Guid)
- `ClientId` (FK User)
- `Category` (Enum)
- `Status` (Enum: Created, Matching, Scheduled, InProgress, Completed, Validated, Canceled)
- `Description` (String)
- `AddressStreet` (String)
- `AddressCity` (String)
- `AddressZip` (String)
- `Latitude` (Double)
- `Longitude` (Double)
- `CreatedAt` (DateTime)
- `ScheduledAt` (DateTime?)

### 4. Proposal (Proposta/Bid)
- `Id` (Guid)
- `RequestId` (FK ServiceRequest)
- `ProviderId` (FK User)
- `Status` (Enum: Pending, Accepted, Rejected)
- `EstimatedValue` (Decimal?)
- `CreatedAt` (DateTime)

### 5. Review (Avaliação)
- `Id` (Guid)
- `RequestId` (FK ServiceRequest)
- `Rating` (Int: 1-5)
- `Comment` (String)
- `CreatedAt` (DateTime)

## Endpoints da API (v1)

### Autenticação (`/api/auth`)
- `POST /register`: Cria novo usuário (Cliente ou Prestador).
- `POST /login`: Retorna JWT Token.

### Usuários (`/api/users`)
- `GET /me`: Dados do usuário logado.
- `PUT /me`: Atualizar perfil.
- `POST /me/provider-profile`: Criar/Atualizar perfil de prestador.

### Pedidos (`/api/requests`)
- `POST /`: Criar novo pedido (Cliente).
- `GET /`: Listar meus pedidos.
- `GET /{id}`: Detalhes do pedido.
- `PATCH /{id}/cancel`: Cancelar pedido.
- `POST /{id}/complete`: Marcar como concluído (Prestador).
- `POST /{id}/validate`: Validar conclusão (Cliente).

### Prestador (`/api/provider`)
- `GET /leads`: Listar pedidos disponíveis na região (baseado no raio).
- `POST /leads/{id}/accept`: Aceitar um pedido.

## Fluxo de Trabalho (Exemplo Simplificado)

1.  **Cliente** faz `POST /api/requests` criando um pedido. Status: `Created`.
2.  **Prestador** faz `GET /api/provider/leads` e vê o pedido (filtrado por raio).
3.  **Prestador** faz `POST /api/provider/leads/{id}/accept`. Status do pedido muda para `Matching` ou `Scheduled` (dependendo da regra de negócio de aceite automático).
4.  Após o serviço, **Prestador** chama `POST /api/requests/{id}/complete`. Status: `Completed`.
5.  **Cliente** revisa e chama `POST /api/requests/{id}/validate`. Status: `Validated`. Avaliação (`POST /api/reviews`) é liberada.
