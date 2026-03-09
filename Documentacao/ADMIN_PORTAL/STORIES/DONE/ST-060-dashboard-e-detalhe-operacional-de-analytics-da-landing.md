# ST-060 - Dashboard e detalhe operacional de analytics da landing

## Como
time admin/comercial/operacao

## Eu quero
acompanhar em telas proprias do Portal Admin o comportamento da landing publica e a qualidade do trafego captado

## Para
agir sobre engajamento, conversao e origem dos acessos com mais clareza operacional.

## Criterios de aceite

1. O Portal Admin exibe um modulo `Analytics Landing` com acesso por menu lateral.
2. A tela principal mostra KPI, funil, distribuicao geografica, ranking de paginas/eventos e heatmap fase 1 agregado.
3. Os filtros da tela utilizam drawer/offcanvas no padrao do portal.
4. Existe tela de detalhe por sessao com timeline de eventos, metadados tecnicos e correlacao com lead quando houver.
5. Toda nomenclatura visivel ao usuario fica em PT-BR e nao expoe enums crus.

## Tasks

- [x] adicionar endpoints autenticados para overview e detalhe operacional;
- [x] criar view models e cliente web admin para analytics da landing;
- [x] adicionar item de menu `Analytics Landing`;
- [x] criar tela de overview com drawer de filtros;
- [x] criar tela de detalhe por sessao;
- [x] documentar QA/operacao e troubleshooting do modulo.
