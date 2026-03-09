# ST-069 - Menu central e segunda visao operacional no Fire TV

## Como
operacao/lideranca/comercial

## Eu quero
abrir um menu central no app Fire TV e escolher entre a visao da landing e uma visao operacional do marketplace

## Para
usar a TV tanto como painel de growth quanto como painel executivo operacional sem depender do portal admin completo.

## Criterios de aceite

1. Apos splash e autenticacao admin, o app Fire TV exibe um menu central com dois botoes: `Metricas da landing` e `Visao operacional`.
2. Cada view possui botao `Voltar` para retornar ao menu central e botao `Sair` para encerrar a sessao.
3. A nova visao operacional exibe, em layout 10-foot, os blocos:
   - logo;
   - status/health dos portais e API;
   - horario;
   - `Servicos Hoje`;
   - `Profissionais Cadastrados`;
   - `Atendimentos`;
   - `Avaliacao Media`;
   - `Servicos por categoria`;
   - `Mapa de atendimentos`;
   - `Grafico de barras de pedidos e atendimentos por dia`;
   - `Servicos concluidos`;
   - `SLA`;
   - `Receita Mensal`;
   - `Chamados Cancelados`.
4. A API expõe um endpoint admin dedicado para a visao operacional com payload otimizado para TV.
5. Parametros funcionais da visao operacional permanecem persistidos em banco via secao `FireTvDashboard`, com fallback seguro e edicao pela tela `Configuracoes` do Admin.

## Tasks

- [x] ampliar o runtime config `FireTvDashboard` com flags de views, historico operacional, limites do mapa e timeout de health check;
- [x] criar DTOs e endpoint `GET /api/admin/fire-tv/operations-dashboard` para snapshot operacional;
- [x] reaproveitar metricas do `AdminDashboardService` e `CoverageMap` para montar KPIs, mapa, categorias e serie diaria;
- [x] adicionar menu central ao app TV com navegacao `Landing` <-> `Operacao`;
- [x] criar tela operacional 10-foot inspirada no cockpit de TV enviado como referencia;
- [x] atualizar runbook, indice e changelog da trilha Fire TV.
