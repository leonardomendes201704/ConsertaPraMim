# ST-006 - Orquestrador OpenAI com contexto historico e linguagem humana

Status: Backlog  
Epic: EPIC-002

## Objetivo

Implementar o orquestrador de IA para responder em linguagem natural, manter contexto da conversa e acionar intents de negocio com baixo atrito para o cliente.

## Criterios de aceite

- Integracao com OpenAI via configuracao segura por `env/secrets`.
- Prompt base define persona de atendimento humano, objetivo e limites operacionais.
- IA recebe contexto relevante: historico, estado do pedido, dados do cliente e ultimo passo.
- Respostas evitam tom robotico e usam linguagem clara/orientada a acao.
- Quando houver incerteza, IA solicita dados faltantes em vez de inventar resposta.
- Log de observabilidade registra latencia, modelo, tokens e resultado da intencao.

## Tasks

- [ ] Criar gateway OpenAI no backend com retries, timeout e tratamento de erro.
- [ ] Definir prompt system e politicas de resposta natural focadas em atendimento.
- [ ] Definir estrutura de saida controlada (intent, entidades, proximo passo, mensagem ao cliente).
- [ ] Implementar montagem de contexto historico por cliente e conversa.
- [ ] Implementar fallback quando IA falhar (mensagem segura + tentativa de recuperacao).
- [ ] Criar mecanismo de limitacao de custo (tokens maximos, truncamento de historico e cache quando aplicavel).
- [ ] Instrumentar logs/metricas para auditoria de qualidade e custo.
- [ ] Criar testes unitarios para parsing de intents e validacao de fallback.
- [ ] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [ ] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.
