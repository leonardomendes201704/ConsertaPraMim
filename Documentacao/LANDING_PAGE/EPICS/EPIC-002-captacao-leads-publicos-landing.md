# EPIC-002 - Captacao de leads publicos na landing

Status: DONE
Trilha: LANDING_PAGE, API

## Objetivo

Transformar a landing publica em um ponto real de aquisicao de demanda, permitindo captar leads de clientes interessados e prestadores parceiros sem enviar o usuario diretamente para os portais transacionais.

## Problema de negocio

- Os CTAs principais da landing hoje pulam diretamente para os portais, reduzindo a capacidade de capturar intencao comercial antes do cadastro/login.
- A operacao nao possui uma tabela dedicada para medir volume, origem, localidade e qualidade dos contatos recebidos pela landing.
- Informacoes tecnicas de navegacao (IP, user-agent, UTM, referer, pagina, host) nao ficam consolidadas em um registro comercial para analytics e follow-up.

## Resultado esperado

- `Encontrar Profissional` e `Cadastrar-se como Parceiro` passam a abrir formularios ocultos no final da landing.
- Cada submissao grava um lead em tabela dedicada, com origem `Client` ou `Provider`, dados de negocio e metadados tecnicos.
- A captura acontece por endpoint publico/anônimo na API, com Swagger, CORS, CSP e deploy alinhados ao dominio `www.consertapramim.com`.
- A trilha documental fica completa com Epic, Story, Tasks, manual QA e diagrama Mermaid.

## Metricas de sucesso

- volume diario de leads captados pela landing por origem;
- distribuicao geografica (cidade/UF/bairro) dos leads;
- taxa de envio concluido por CTA (`cliente` x `prestador`);
- disponibilidade do endpoint publico de captura sem erro de CORS/CSP em producao.

## Escopo

### Inclui

- formularios ocultos no fim da landing para cliente e prestador;
- endpoint publico/anônimo de captura de leads;
- persistencia em tabela dedicada com dados comerciais e tecnicos;
- CORS/CSP/compose ajustados para o envio browser -> API;
- documentacao operacional e de backlog da trilha.

### Nao inclui

- CRM completo ou fila operacional de leads no portal admin;
- automacao de contato por e-mail/WhatsApp;
- distribuicao automatica do lead para prestadores;
- dashboards analiticos administrativos dedicados.

## Historias vinculadas

- ST-002 - Captura de leads cliente/prestador na landing publica.
