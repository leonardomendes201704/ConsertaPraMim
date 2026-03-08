# ST-002 - Captura de leads cliente/prestador na landing publica

Status: DONE
Epic: EPIC-002

## Objetivo

Converter os dois CTAs principais da landing em pontos de captacao de leads, exibindo formularios ocultos no final da pagina e persistindo o contato com origem, localidade e metadados tecnicos.

## Criterios de aceite

- O botao `Encontrar profissional` nao redireciona mais para o portal cliente; ele exibe o formulario de lead `Cliente` no final da landing e faz scroll ate a secao.
- O botao `Cadastrar-se como parceiro` nao redireciona mais para o portal prestador; ele exibe o formulario de lead `Prestador` no final da landing e faz scroll ate a secao.
- Os formularios iniciam ocultos e apenas o fluxo clicado fica ativo/visivel.
- A submissao chama um endpoint publico/anônimo na API sem depender de autenticacao.
- A tabela de leads registra origem (`Client`/`Provider`), dados de negocio, cidade, bairro, UF e metadados tecnicos como IP, user-agent, referer, host, path, query/UTM e idioma do browser.
- A landing nao pode apresentar erro de `Mixed Content`, `CSP` ou `CORS` ao enviar o formulario em `https://www.consertapramim.com`.
- Swagger, manual QA/Operacao, changelog e diagrama Mermaid atualizados no mesmo ciclo.

## Tasks concluidas

- [x] Registrar Epic/Story/Tasks da trilha de captacao de leads da landing.
- [x] Criar entidade/tabela de leads com origem e metadados tecnicos.
- [x] Implementar service e endpoint publico/anônimo de captura na API.
- [x] Ajustar CORS/Swagger/deploy para a landing conversar com a API publicada.
- [x] Refatorar a landing para abrir formularios ocultos e enviar os dados via browser.
- [x] Atualizar manual QA/Operacao, diagrama Mermaid e changelog com o fluxo final.

## Escopo entregue

- CTAs principais da landing passaram a abrir formularios ocultos de captacao no fim da pagina;
- formulario `Cliente` registra demanda comercial com nome, contato, localidade e contexto do servico;
- formulario `Prestador` registra interesse de parceria com especialidade, localidade e dados operacionais iniciais;
- API publica `POST /api/landing-leads/public` captura os dados sem autenticacao;
- tabela dedicada `LandingLeads` persiste origem, dados comerciais, localidade e metadados tecnicos de navegacao;
- CORS/CSP/compose da landing e da API foram alinhados para o envio browser -> API em HTTPS;
- Swagger, manual QA/Operacao, diagrama Mermaid e testes de regressao foram entregues no mesmo ciclo.

## Validacao esperada

- clicar em `Encontrar profissional` abre a secao `#captacao` com o formulario `Cliente` ativo;
- clicar em `Cadastrar-se como parceiro` abre a secao `#captacao` com o formulario `Prestador` ativo;
- envio de ambos os formularios retorna confirmacao sem refresh total da pagina;
- `origin` e aceito como `Client`/`Provider` no payload JSON da landing;
- lead persistido registra cidade, UF, bairro e metadados tecnicos como IP, `User-Agent`, `Referer` e UTM;
- `https://www.consertapramim.com` nao apresenta erros de `Mixed Content`, `CORS` ou `Content-Security-Policy` durante a captura.
