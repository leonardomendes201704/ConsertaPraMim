# ST-052 - AI Copilot Growth (Operacao)

## Objetivo

Orientar o uso operacional do modulo `AI Copilot Growth` no portal admin para diagnostico de funil e liquidez com suporte de IA.

## Onde configurar

1. Portal Admin -> menu lateral `AI Copilot Growth`.
2. Bloco `Configuracao OpenAI`.
3. Campos obrigatorios para operacao:
- `Copiloto habilitado`
- `Modelo`
- `OpenAI API key`

## Persistencia e seguranca da API key

- A chave e persistida no backend no snapshot `SystemSettings` (`admin.growth.ai.snapshot.v1`).
- A UI nunca retorna chave em texto aberto: somente `ApiKeyMasked`.
- Se o campo de chave ficar em branco ao salvar, o backend preserva a chave anterior.

## Fluxo recomendado de uso semanal

1. Definir recorte (`De/Ate`, categoria, cidade, SLAs).
2. Executar `Gerar analise IA`.
3. Revisar:
- `Resumo executivo`
- `Insights de funil`
- `Insights de liquidez`
- `Riscos`
- `Acoes recomendadas`
4. Registrar decisoes no ritual semanal/mensal do `Cockpit Growth`.

## Contratos de API usados

- `GET /api/admin/growth/ai/snapshot`
- `PUT /api/admin/growth/ai/settings`
- `POST /api/admin/growth/ai/analyze`

## Troubleshooting rapido

- `growth_ai_not_configured`: API key nao cadastrada.
- `growth_ai_disabled`: modulo esta desabilitado.
- `growth_ai_gateway_error`: falha de comunicacao/credencial OpenAI; validar modelo, chave e conectividade de saida da API.
