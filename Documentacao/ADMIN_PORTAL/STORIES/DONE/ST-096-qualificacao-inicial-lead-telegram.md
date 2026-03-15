# ST-096 - Qualificacao inicial do lead Telegram com cidade, categoria e intencao

Status: Done
Epic: EPIC-TELEGRAM-002

## Objetivo

Melhorar a qualificacao inicial do lead Telegram para que o funil e o Chatwoot recebam mais contexto de negocio logo no inicio da conversa.

## Entrega realizada

- O `TelegramBridge` passou a extrair cidade/regiao, categoria, CEP e intencao a partir do texto livre recebido no bot.
- O ACK inicial do bot agora orienta o usuario a informar cidade, tipo de servico e objetivo principal, com texto adaptado para `clientes` e `prestadores`.
- O mesmo lead do CPM Full passou a receber `ServiceCategory`, `City`, `StatusNote` e `InternalNotes` mais ricos, sem duplicar lead nem criar schema novo.
- O roteamento `clientes` x `prestadores` ficou mais preciso com heuristica baseada em onboarding, autoidentificacao profissional e categoria tecnica.

## Criterios de aceite atendidos

- O fluxo de cliente coleta cidade, categoria e intencao principal por pergunta guiada do ACK e extracao de texto livre nas mensagens subsequentes.
- O fluxo de prestador identifica melhor regiao, categoria tecnica e objetivo principal de cadastro/parceria.
- O lead chega ao CPM Full com dados mais uteis para operacao em `ServiceCategory`, `City`, `StatusNote` e `InternalNotes`.
- O roteamento `clientes` x `prestadores` ficou menos dependente de uma heuristica simples baseada em poucas palavras fixas.

## Tasks

- [x] Definir campos minimos de qualificacao por jornada.
- [x] Ajustar a conversa do bot para coletar cidade, categoria e intencao.
- [x] Projetar os dados no lead do CPM Full e no Chatwoot.
- [x] Refinar o roteamento de board/etapa inicial.
- [x] Atualizar QA e troubleshooting da trilha.
