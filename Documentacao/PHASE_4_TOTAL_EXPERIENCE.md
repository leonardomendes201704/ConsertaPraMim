# Fase 4: Experiência Total e Fidelização

Este plano expande o ecossistema ConsertaPraMim para fechar o ciclo de confiança entre clientes e prestadores, introduzindo transações financeiras e uma interface de cliente de alto nível.

## 📋 Lista de Tarefas (Pillars)

### 👑 Pillar 1: Portal do Cliente VIP (Foco em Conversão)
Transformar a jornada do cliente em algo simples e encantador.
- [ ] **Novo Projeto:** Criar `ConsertaPraMim.Web.Client` (MVC) seguindo o padrão do Provider.
- [ ] **Wizard de Solicitação:** Formulário multi-etapas com:
    - Etapa 1: Categoria e Descrição Visual.
    - Etapa 2: Fotos (Drag & Drop).
    - Etapa 3: Localização (Integração com Mapa).
- [ ] **Dashboard do Cliente:** 
    - Listagem de "Meus Pedidos".
    - Comparador de Propostas (ver preço vs avaliação do prestador).
- [ ] **Notificações Push (SignalR):** Alerta em tempo real quando uma nova proposta chegar.

### 💳 Pillar 2: Fintech e Segurança (Stripe & Escrow)
Garantir que o prestador receba e o cliente tenha segurança.
- [ ] **Integração Backend:** Adicionar `IPaymentService` usando Stripe/Mercado Pago.
- [ ] **Fluxo de Escrow:**
    - Bloqueio do pagamento ao aceitar proposta.
    - Liberação automática após confirmação de conclusão.
- [ ] **Lógica de Taxas:** Implementar desconto de 10% da plataforma no repasse ao prestador.

### 🛡️ Pillar 3: Central de Confiança e Chat
Reduzir o atrito e aumentar a segurança.
- [ ] **Chat Interno:** Sistema de mensagens em tempo real para dúvidas pré-contratação.
- [ ] **Selo de Verificado:**
    - Fluxo de upload de documentos (RG/CPF) no Perfil do Prestador.
    - Action de Aprovação no `AdminController`.
- [ ] **Galeria de Portfólio:** Aba no perfil do prestador para exibir "Trabalhos Realizados".

---

## 🚀 Próximos Passos Imediatos
1. Iniciar a estrutura do **Portal do Cliente**.
2. Criar a base do **Chat Interno** para facilitar a negociação.
