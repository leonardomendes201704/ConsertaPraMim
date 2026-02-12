# Status de Desenvolvimento: Portal Web do Prestador

Este documento rastreia o progresso da "Contingência Web" para os prestadores de serviço.

## 🛠️ Progresso Geral
- **Fase 1: Setup**: 100% ✅
- **Fase 2: Auth**: 100% ✅
- **Fase 3: Matches**: 100% ✅
- **Fase 4: Gestão**: 100% ✅
- **Fase 5: Perfil**: 100% ✅

---

## 📋 Lista de Tarefas (Tasks)

### 🚀 Fase 1: Setup do Projeto
- [x] Criar projeto MVC .NET 9.0 `./src/ConsertaPraMim.Web.Provider`
- [x] Configurar `Program.cs` para usar os serviços da camada Application.
- [x] Integrar SQLite (partilhando o banco do backend).
- [x] Configurar layout base com Bootstrap 5.

### 🔐 Fase 2: Autenticação (Acesso)
- [x] Controller `AccountController` (Login/Register).
- [x] Logout e proteção de rotas `[Authorize]`.
- [x] Toast notifications para erros/sucessos.

### 🛠️ Fase 3: Oportunidades (Matching)
- [x] View de listagem de pedidos disponíveis (Matching logic).
- [x] View de detalhes e envio de Proposta.

### 📈 Fase 4: Serviços e Histórico
- [x] Gestão de propostas enviadas.
- [x] Dashboard com resumo de atividades.

### ⚙️ Fase 5: Configurações
- [x] Página de Perfil e Radar de Atendimento.
- [x] Gestão de Categorias (Checkbox list).
