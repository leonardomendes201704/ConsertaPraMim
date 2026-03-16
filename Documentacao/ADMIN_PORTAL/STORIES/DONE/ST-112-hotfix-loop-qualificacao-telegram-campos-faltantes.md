# ST-112 - Hotfix do loop de qualificacao do Telegram com pergunta por campo faltante

Status: Done
Epic: EPIC-JORNADA-001

## Objetivo

Eliminar o comportamento em loop do bot Telegram quando o usuario responde a qualificacao por etapas, fazendo o bridge perguntar apenas os campos realmente faltantes e aceitar respostas curtas de localizacao como `Praia Grande`.

## Criterios de aceite

- O bridge reconhece respostas curtas de localizacao quando o usuario envia apenas cidade ou bairro.
- O bot nao repete indefinidamente o bloco generico `cidade, tipo de servico e o que voce precisa resolver`.
- O contrato interno do Telegram passa a devolver `QualificationStatus`, `ConfirmationPrompt` e `MissingRequiredFields`.
- O bridge pergunta apenas os campos pendentes em PT-BR, com texto contextual.
- A trilha continua pronta para aproveitar OpenAI na qualificacao quando a chave estiver configurada em runtime.

## Tasks

- [x] Ajustar extracao deterministica de cidade/regiao para respostas curtas.
- [x] Propagar estado de qualificacao do CPM Full para o TelegramBridge.
- [x] Montar prompts por campo faltante em vez de repetir o bloco fixo.
- [x] Cobrir regressao para resposta `Praia Grande` e para captura de telefone seguida de qualificacao incremental.
- [x] Atualizar changelog, indice e manual operacional no mesmo ciclo.

## Entrega implementada

- O `TelegramInboundUpdateProcessor` ganhou heuristica para extrair localizacao de respostas curtas no inicio da mensagem.
- O `TelegramLeadAutomationService` passou a devolver `QualificationStatus`, `ConfirmationPrompt` e `MissingRequiredFields` no retorno do upsert interno.
- O `TelegramBridge` passou a usar esses campos para decidir a proxima resposta, perguntando apenas o que ainda falta.
- O bot deixou de depender exclusivamente do texto bruto da ultima mensagem para decidir se continua qualificando, se pede confirmacao ou se apenas oferece envio opcional de e-mail.
