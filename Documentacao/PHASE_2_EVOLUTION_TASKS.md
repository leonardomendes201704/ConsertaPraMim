# Evolução do Sistema - Fase 2: Ciclo de Serviço e Notificações

Este documento descreve as próximas etapas para amadurecer o ConsertaPraMim, focando no fechamento do ciclo de serviço e melhoria da comunicação.

## 📋 Lista de Tarefas (Roadmap)

### 🗓️ Task 1: Gestão de Agenda (Ciclo de Serviço) [CONCLUÍDO ✅]
Focar em como o prestador gerencia os serviços que já foram aceitos.
- [x] Implementar `GetScheduledByProviderAsync` no `ServiceRequestService`.
- [x] Criar Action `Agenda` no `ServiceRequestsController`.
- [x] Desenvolver View "Minha Agenda" com foco em contatos e datas.
- [x] Implementar botão de redirecionamento para WhatsApp do Cliente.
- [x] Criar funcionalidade "Finalizar Serviço" (Atualização de Status).

### 🔔 Task 2: Subsistema de Notificações (Mock) [CONCLUÍDO ✅]
Melhorar a percepção de interatividades no sistema.
- [x] Definir `INotificationService` na camada Application.
- [x] Criar `EmailNotificationService` (Mock) na Infrastructure.
- [x] Integrar disparo ao enviar nova proposta (Avisa o Cliente).
- [x] Integrar disparo ao aceitar proposta (Avisa o Prestador).

### 🛡️ Task 3: Painel Administrativo (Backoffice) [CONCLUÍDO ✅]
Monitoramento global do sistema.
- [x] Criar `AdminController` com restrição de Role.
- [x] Dashboard Admin com stats: Total de Usuários, Pedidos Ativos, Volume de Propostas.
- [x] Lista de Usuários com opção de Ativar/Desativar.

### 📸 Task 4: Upload de Imagens e Perfil Rico [CONCLUÍDO ✅]
Aumentar a confiança entre cliente e prestador.
- [x] Configurar serviço de armazenamento local de mídias.
- [x] Suporte a imagem no Pedido de Serviço (Preview na lista).
- [x] Foto de Perfil para o Prestador.

---

## 🎯 Ponto de Partida Atual
Vamos iniciar pela **Task 1: Gestão de Agenda**, garantindo que o prestador consiga finalizar um ciclo completo de serviço.
