# ST-077 - Mapa relacional das tabelas do banco no Portal Admin

## Como
admin tecnico(a) e QA do ecossistema ConsertaPraMim

## Eu quero
abrir no Portal Admin uma tela com todas as tabelas e os relacionamentos do banco

## Para
analisar estrutura relacional e impacto de mudancas sem depender de consulta manual no banco.

## Criterios de aceite

1. O menu lateral do Portal Admin deve exibir o item `Mapa de Dados`.
2. A tela `Mapa de Dados` deve listar tabelas (schema.nome) com indicacao de colunas PK/FK.
3. A tela deve renderizar um diagrama ER Mermaid com os relacionamentos de FK do modelo atual.
4. A tela deve exibir grade de relacionamentos com principal/dependente, colunas e regras (required/unique/delete behavior).
5. O modulo deve possuir cobertura de teste unitario para geracao de inventario + mermaid.
6. O manual QA/Operacao e o changelog devem refletir a entrega.
7. O inventario deve permitir leitura por dominio/contexto e o preview deve alternar entre `Fluxo tecnico` e `ER por tabelas` nos recortes suportados.

## Tasks

- [x] criar `AdminDatabaseSchemaController` com rota `Index` protegida por policy `AdminOnly`;
- [x] implementar `AdminDatabaseSchemaService` para ler metadados do `ConsertaPraMimDbContext` e montar tabelas + relacionamentos;
- [x] criar view `Views/AdminDatabaseSchema/Index.cshtml` com lista de tabelas, diagrama Mermaid (pan/zoom) e tabela de FKs;
- [x] adicionar item `Mapa de Dados` no menu lateral (`Views/Shared/_Layout.cshtml`);
- [x] registrar DI em `Program.cs` e ajuste de referencia de projeto para `ConsertaPraMim.Infrastructure`;
- [x] adicionar teste `AdminDatabaseSchemaServiceTests` na suite unit;
- [x] atualizar manual QA/Operacao com novo caso `QA-ADM-073`, smoke, regressao e troubleshooting;
- [x] registrar entrada no changelog em `Released`;
- [x] corrigir carregamento de pan/zoom com asset local `~/lib/svg-pan-zoom/svg-pan-zoom.min.js` nos modulos `Diagramas Mermaid` e `Mapa de Dados`;
- [x] adicionar modos de layout no `Mapa de Dados` (macro por dominio, visao geral e recortes por dominio) com zoom inicial otimizado para uso do canvas;
- [x] aumentar altura efetiva do canvas de preview;
- [x] corrigir abertura com clipping vertical (topo cortado) recalibrando render base do SVG e fluxo inicial de `fit/center`;
- [x] tornar cards de `Tabelas mapeadas` clicaveis para gerar diagrama focado na tabela selecionada e seus relacionamentos diretos;
- [x] corrigir foco por tabela para neutralizar `max-width` inline do Mermaid e evitar SVG encolhido no preview;
- [x] exibir colunas com tipo SQL e marcadores `PK/FK/nullability` no foco por tabela, corrigindo metadado que retornava `0 colunas` em schema default;
- [x] estilizar o foco por tabela com cards em layout visual de diagrama ER, alinhando nome/tipo de coluna e simplificando conectores.
- [x] agrupar o inventario por dominio/contexto e adicionar seletor de estilo do preview (`Fluxo tecnico`/`ER por tabelas`) para os recortes do schema.
