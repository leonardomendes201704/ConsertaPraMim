# Especificação: Portal do Prestador (Web)

## 🎯 Objetivo
Desenvolver uma interface web robusta e funcional para que os prestadores de serviço possam gerenciar suas atividades fora do aplicativo mobile. Será uma ferramenta de contingência e gestão administrativa.

## 🛠️ Stack Tecnológica
- **Framework**: .NET 9.0 ASP.NET Core MVC.
- **View Engine**: Razor (MVC).
- **Estilização**: Bootstrap 5 + Vanilla CSS para toques premium.
- **Ícones**: FontAwesome ou Bootstrap Icons.
- **Interação**: JavaScript Minimalista (Vanilla JS) + validações nativas do ASP.NET (jQuery Validation Unobtrusive).
- **Consumo de Dados**: Utilização direta da camada `ConsertaPraMim.Application` (compartilhando a lógica de negócio do Backend).

## 📂 Estrutura de Páginas (Sitemap)

### 1. Área Pública
- **Landing/Login**: Acesso com e-mail e senha.
- **Cadastro**: Fluxo específico para prestadores (incluindo seleção de categorias e raio de atendimento).

### 2. Painel Principal (Dashboard)
- Resumo de propostas enviadas.
- Status atual do prestador (Online/Offline/Em Atendimento).
- Próximos serviços agendados.

### 3. Gestão de Pedidos (Oportunidades)
- Lista de pedidos "Matching" (proximidade e categoria).
- Detalhes do pedido (descrição, cliente, localização no mapa).
- Formulário para envio de **Proposta**.

### 4. Meus Serviços
- Histórico de serviços (Agendados, Em Andamento, Finalizados).
- Visualização de avaliações recebidas.

### 5. Configurações de Perfil
- Edição de dados pessoais e contato.
- Atualização do **Raio de Atendimento** e **Localização Base**.
- Gestão de Categorias atendidas.

## 🎨 Design System (Bootstrap Premium)
- **Tema**: Dark/Light mode (priorizando modo claro limpo com detalhes em azul/cinza).
- **Cards**: Para representar cada pedido de serviço de forma clara.
- **Responsividade**: Mobile-first (deve funcionar perfeitamente em navegadores de celular).

---

## 🏗️ Plano de Implementação (Tasks)

### Fase 1: Setup e Estrutura Inicial
- [ ] Criar projeto `ConsertaPraMim.Web.Provider` (ASP.NET Core MVC).
- [ ] Adicionar referências aos projetos `Application` e `Domain`.
- [ ] Configurar Injeção de Dependência e Authentication (Shared Cookie Auth com a lógica do Backend).
- [ ] Definir Layout base (Navbar, Sidebar, Footer).

### Fase 2: Autenticação e Registro
- [ ] Implementar páginas de Login e Cadastro de Prestador.
- [ ] Validações de formulário com DataAnnotations.

### Fase 3: Dashboard e Matching
- [ ] Página inicial com lista de pedidos disponíveis (Logic de Matching).
- [ ] Botão "Quero este serviço" para abrir formulário de proposta.

### Fase 4: Gestão de Propostas e Serviços
- [ ] Listagem de propostas enviadas e status (Aceita/Pendente).
- [ ] Fluxo de finalização de serviço (marcar como concluído).

### Fase 5: Perfil e Configurações
- [ ] Tela de perfil com mapa para definir localização base.
- [ ] Edição de categorias de serviço.
