# EPIC-TELEGRAM-002 - Enriquecimento Operacional do Bot Telegram no CPM e Chatwoot

## 1. Metadados da EPIC

- Epic ID: `EPIC-TELEGRAM-002`
- Produto: `ConsertaPraMim`
- Data de criacao: `2026-03-15`
- Prioridade: `Alta`
- Status atual: `In Progress`
- Time alvo: `Backend`, `TelegramBridge`, `CPM Full`, `Chatwoot`, `QA`, `Operacao`
- Objetivo macro: elevar a qualidade operacional da trilha publicada `Telegram -> CPM Full -> Chatwoot`, capturando melhor os dados do lead, refinando qualificacao/handoff e criando observabilidade orientada a negocio.

## 2. Contexto atual

- O bot Telegram ja cria ou atualiza lead no CPM Full para `clientes` e `prestadores`.
- A trilha ja abre ou reaproveita conversa humana no Chatwoot, espelha mensagens bidirecionais e suporta handoff humano.
- O bot ja opera em `Webhook` publicado por `https://telegram.consertapramim.com`.
- O principal gap atual e de qualidade do lead e governanca operacional:
  - muitos usuarios chegam sem telefone e sem e-mail no primeiro contato;
  - a classificacao inicial de `cliente` x `prestador` ainda e heuristica e pouco enriquecida;
  - as regras de handoff humano ainda sao mais tecnicas do que operacionais;
  - a observabilidade existente e forte em diagnostico tecnico, mas ainda fraca para leitura de negocio e rotina do time.

## 3. Objetivos de negocio

1. Aumentar a taxa de leads Telegram com telefone validado e contexto minimo aproveitavel.
2. Melhorar a qualificacao inicial do lead com cidade, categoria e intencao.
3. Tornar previsivel e auditavel o handoff entre bot e humano.
4. Dar visibilidade operacional e gerencial ao canal Telegram, com metricas de conversao, tempo e gargalos.

## 4. Escopo

### 4.1 Em escopo

1. Captura de telefone via compartilhamento nativo do Telegram e fallback textual.
2. Captura opcional de e-mail e atualizacao automatica do lead/contato no Chatwoot.
3. Coleta guiada de cidade, categoria e intencao principal no inicio da conversa.
4. Refinamento do roteamento `clientes` x `prestadores` com base no contexto coletado.
5. Regras explicitas para handoff humano, pausa do bot e retomada controlada.
6. Observabilidade de negocio da trilha Telegram com indicadores operacionais e gerenciais.

### 4.2 Fora de escopo

1. Criar um novo canal alem de Telegram.
2. Reescrever integralmente o motor conversacional atual do bot.
3. Transformar o Chatwoot em sistema de verdade do funil.
4. Criar CRM externo ou pipeline paralelo fora do CPM Full.
5. Campanhas outbound em massa pelo Telegram.

## 5. Diretrizes tecnicas

1. O CPM Full continua como sistema de verdade do lead e do funil.
2. O Chatwoot continua como camada de atendimento humano.
3. Toda captura adicional do Telegram deve atualizar o mesmo lead ja existente, sem abrir trilha paralela.
4. Dados pessoais devem respeitar a sanitizacao ja implementada na trilha Telegram.
5. Regras de handoff nao podem quebrar o espelhamento existente nem reativar o bot automaticamente sem criterio explicito.
6. Indicadores de negocio devem reaproveitar ao maximo a telemetria e os vinculos tecnicos ja existentes (`ChatbotConversationId`, `LeadId`, `ChatwootConversationId`).

## 6. Historias e tasks detalhadas

## US-01 / ST-095 - Captura de contato no primeiro atendimento do Telegram

### Descricao

Como operacao, queremos coletar telefone e, quando possivel, e-mail no primeiro contato do Telegram para enriquecer automaticamente o lead no CPM Full e o contato no Chatwoot.

### Status

- Concluida em `2026-03-15`.

### Criterios de aceite

1. O bot consegue solicitar telefone com botao nativo de compartilhamento de contato.
2. Quando o usuario compartilhar telefone, o lead do CPM Full e o contato do Chatwoot sao atualizados automaticamente.
3. O e-mail pode ser solicitado como etapa complementar, sem bloquear o fluxo principal.
4. O funil e o Chatwoot deixam de depender apenas do identificador tecnico do Telegram quando o contato real for fornecido.

### Tasks

- `TASK-01.01` Concluida. O melhor momento definido foi logo apos o bootstrap inicial do lead Telegram, sem bloquear a triagem.
- `TASK-01.02` Concluida. O bridge agora envia `request_contact` no primeiro ACK do lead e aceita fallback textual seguro para telefone/e-mail.
- `TASK-01.03` Concluida. Telefone/e-mail passam a ser persistidos no vinculo tecnico Telegram e projetados no mesmo lead do CPM Full sem limpar dados ja capturados.
- `TASK-01.04` Concluida. A sync existente do Chatwoot reaproveita o contato tecnico ja vinculado e o enriquece com o telefone/e-mail real quando informado depois.
- `TASK-01.05` Concluida. Foram adicionados testes de regressao, atualizacao do manual operacional e registro em changelog/story.

### Entrega realizada

1. O `ConsertaPraMim.Web.TelegramBridge` passou a reconhecer `message.contact`, solicitar telefone com teclado nativo `request_contact` e aceitar fallback textual seguro para telefone/e-mail.
2. O `ConsertaPraMim.Web.CpmFull` agora persiste `ClientPhone` no vinculo `dbo.cpm_web_telegram_funil_links`, atualiza o mesmo lead com telefone/e-mail sem apagar dados anteriores e exibe o telefone mascarado no detalhe do lead.
3. O enriquecimento subsequente do contato reaproveita a mesma conversa/contato tecnico do Chatwoot, reduzindo a dependencia exclusiva de `TelegramChatId`/`ChatbotConversationId`.

## US-02 / ST-096 - Qualificacao inicial do lead Telegram

### Descricao

Como operacao, queremos qualificar melhor o lead Telegram no inicio da conversa, coletando cidade, categoria e intencao para roteamento mais preciso no funil.

### Status

- Planejada.

### Criterios de aceite

1. O bot coleta cidade e categoria de forma objetiva para o fluxo de cliente.
2. O bot identifica melhor onboarding de prestador, categoria tecnica e regiao de atuacao.
3. O lead chega ao CPM Full com contexto mais aproveitavel para operacao e atendimento.
4. O board `clientes` x `prestadores` fica menos dependente de heuristica simples de texto livre.

### Tasks

- `TASK-02.01` Definir os campos minimos de qualificacao inicial por jornada (`cliente` x `prestador`).
- `TASK-02.02` Ajustar prompts/etapas do bot para coletar cidade, categoria e intencao.
- `TASK-02.03` Projetar os novos dados no lead, no historico e nos atributos do Chatwoot.
- `TASK-02.04` Refinar o roteamento de board e etapa inicial no funil.
- `TASK-02.05` Cobrir cenarios ambigos e fallback para classificacao manual.

## US-03 / ST-097 - Regras operacionais de handoff entre bot e humano

### Descricao

Como operacao, queremos regras claras de handoff para decidir quando o bot continua, quando transfere para humano e quando pode retomar a conversa.

### Status

- Planejada.

### Criterios de aceite

1. Existem gatilhos claros para handoff automatico ou manual.
2. O bot nao responde enquanto o handoff humano estiver ativo, salvo regra explicita de retomada.
3. O operador entende no CPM Full e no Chatwoot o motivo do handoff e o estado atual da conversa.
4. A retomada do bot, se existir, fica auditavel e controlada.

### Tasks

- `TASK-03.01` Definir gatilhos de handoff por intencao, erro, SLA e comando operacional.
- `TASK-03.02` Persistir estado operacional mais rico para handoff, pausa e retomada.
- `TASK-03.03` Expor os novos estados e comandos no CPM Full para suporte e operacao.
- `TASK-03.04` Ajustar espelhamento e webhook para respeitar as novas regras.
- `TASK-03.05` Cobrir regressao para evitar concorrencia entre bot e humano.

## US-04 / ST-098 - Observabilidade de negocio do canal Telegram

### Descricao

Como gestao e operacao, queremos acompanhar indicadores reais do canal Telegram para medir conversao, gargalos e performance do atendimento.

### Status

- Planejada.

### Criterios de aceite

1. A operacao consegue ver volume de leads, handoffs, tempos e conversoes do canal Telegram.
2. O diagnostico deixa de ser apenas tecnico e passa a apoiar rotina operacional.
3. O CPM Full expõe indicadores suficientes para leitura diaria do canal.
4. O runbook cobre os principais sinais de degradacao operacional e negocio.

### Tasks

- `TASK-04.01` Definir KPIs principais do canal Telegram.
- `TASK-04.02` Criar consultas/agregacoes para tempos, volumes e conversao por jornada.
- `TASK-04.03` Expor visao operacional e gerencial no CPM Full.
- `TASK-04.04` Documentar rotina de acompanhamento e limiares de alerta.
- `TASK-04.05` Cobrir QA funcional e troubleshooting da leitura operacional.

## 7. Sequencia de entrega recomendada

1. `ST-095` - Captura de contato e enriquecimento do lead.
2. `ST-096` - Qualificacao inicial do lead Telegram.
3. `ST-097` - Regras operacionais de handoff.
4. `ST-098` - Observabilidade de negocio do canal Telegram.

## 8. Dependencias externas

1. Bot Telegram publicado e operacional em `Webhook`.
2. CPM Full e Chatwoot publicados e saudaveis.
3. Capacidade do TelegramBridge de enviar teclado com compartilhamento de contato.
4. Acesso operacional do time para validar os fluxos em producao/homologacao.

## 9. Riscos e mitigacoes

1. Risco: o pedido de telefone interromper a conversa cedo demais.
- Mitigacao: solicitar contato no ponto de maior valor, com fallback de continuidade.
2. Risco: enriquecimento duplicar contato no Chatwoot.
- Mitigacao: reaproveitar identificador tecnico existente e fazer merge/upsert controlado.
3. Risco: regras de handoff ficarem confusas para operacao.
- Mitigacao: estados claros, historico e comandos administrativos visiveis.
4. Risco: excesso de telemetria sem leitura util.
- Mitigacao: priorizar poucos KPIs com rotina operacional objetiva.

## 10. Definicao de pronto (DoD) da EPIC

1. Leads Telegram passam a chegar com mais dados reais e menos dependentes de identificador tecnico.
2. A qualificacao inicial do canal melhora a leitura do funil `clientes` x `prestadores`.
3. O handoff entre bot e humano fica previsivel e auditavel.
4. A operacao ganha indicadores de negocio do canal Telegram no CPM Full.
5. Changelog, manual QA/Operacao, epic e stories permanecem atualizados no mesmo ciclo de entrega.
