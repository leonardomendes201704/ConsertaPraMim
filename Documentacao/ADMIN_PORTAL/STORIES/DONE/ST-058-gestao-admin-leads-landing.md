# ST-058 - Gestao administrativa de leads captados pela landing

Status: Done
Epic: EPIC-026

## Objetivo

Permitir que o portal admin liste e detalhe os leads publicos captados na landing, com localidade, origem e metadados tecnicos suficientes para operacao e follow-up.

## Criterios de aceite

- O menu lateral do portal admin exibe o item `Leads Landing`.
- A tela `Leads Landing` apresenta listagem paginada dos leads captados.
- A tela usa drawer/offcanvas para filtros de busca, origem, cidade, UF e periodo.
- A coluna/localidade do grid reflete o endereco real do lead, consolidando bairro, cidade e UF.
- O admin consegue abrir a tela de detalhe de um lead pelo grid.
- O detalhe exibe dados comerciais e metadados tecnicos capturados na landing.
- A API expoe endpoints admin autenticados para lista e detalhe dos leads.
- Swagger, changelog, manual QA e diagrama Mermaid atualizados no mesmo ciclo.

## Tasks

- [x] Registrar Epic/Story/indice da trilha de leads landing no Portal Admin.
- [x] Implementar endpoints administrativos autenticados para lista e detalhe dos leads.
- [x] Implementar modulo `Leads Landing` no portal admin com drawer de filtros.
- [x] Exibir detalhe completo do lead com dados comerciais e tecnicos.
- [x] Atualizar manual QA/Operacao, diagrama Mermaid e changelog.
