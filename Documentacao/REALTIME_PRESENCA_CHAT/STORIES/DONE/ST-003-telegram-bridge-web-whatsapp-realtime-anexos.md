# ST-003 - Telegram Bridge Web com visual WhatsApp, anexos e realtime

Status: Done  
Epic: EPIC-001

## Objetivo

Entregar um portal web dedicado em .NET 8 para atendimento via bot do Telegram, com experiencia de chat estilo WhatsApp, suporte a anexos e sincronizacao em tempo real.

## Criterios de aceite

- Projeto web dedicado criado em `Backend/src/ConsertaPraMim.Web.TelegramBridge` com `TargetFramework = net8.0`.
- Conversas e mensagens sao atualizadas em tempo real via SignalR sem refresh manual.
- Usuario interno consegue enviar mensagem de texto para chat Telegram.
- Usuario interno consegue enviar anexos (imagem, video e documento) para chat Telegram.
- Mensagens e anexos recebidos do Telegram aparecem no painel automaticamente.
- Anexos recebidos sao persistidos localmente em `wwwroot/uploads/telegram-bridge`.
- Layout principal segue linguagem visual inspirada no WhatsApp em desktop e mobile.
- Build do projeto e da solution passam sem erro.

## Tasks

- [x] Criar novo projeto web MVC .NET 8 e incluir na solution.
- [x] Implementar cliente da Telegram Bot API (`getUpdates`, `sendMessage`, `sendPhoto`, `sendDocument`, `sendVideo`, `getFile`).
- [x] Implementar polling em background para processar mensagens do Telegram.
- [x] Implementar armazenamento em memoria de conversas/mensagens com ordenacao por atividade.
- [x] Implementar upload de anexos no painel e envio para o Telegram.
- [x] Implementar download de anexos recebidos do Telegram para storage local.
- [x] Implementar Hub SignalR e broadcast de mensagens/conversas atualizadas.
- [x] Construir UI estilo WhatsApp com lista de conversas, area de mensagens e composer com anexos.
- [x] Validar compilacao do projeto novo e da solution `Backend/src/src.sln`.
- [x] Atualizar changelog, manual QA/Operacao e diagramas Mermaid no mesmo ciclo.
