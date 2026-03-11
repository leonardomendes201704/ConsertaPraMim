# ST-078 - Diagramador ER em ReactFlow no Portal Admin

## Como
admin tecnico(a) e QA do ecossistema ConsertaPraMim

## Eu quero
abrir no Portal Admin uma tela dedicada para diagramar o schema relacional em cards por tabela

## Para
inspecionar dominios, contextos e dependencias diretas do banco sem depender de Mermaid para a leitura ER.

## Criterios de aceite

1. O menu lateral do Portal Admin deve exibir o item `Diagramar ER`.
2. A tela `Diagramar ER` deve usar ReactFlow com assets locais compativeis com a CSP atual do portal.
3. O diagrama deve ser gerado a partir das tabelas e relacionamentos do modelo EF/DbContext atual.
4. A tela deve permitir recorte por dominio/contexto e foco local por tabela com dependencias diretas.
5. Os cards de tabela devem exibir schema, nome, colunas, tipo SQL e marcadores de `PK/FK/NULL`.
6. O canvas deve expor minimapa, controles de navegacao, enquadramento automatico do grafo e botao para reaplicar auto-layout.
7. O modulo deve possuir cobertura unitario-basica do controller e manual/changelog atualizados.

## Tasks

- [x] criar `AdminErDiagramController` com rota `Index` protegida por `AdminOnly`;
- [x] criar a view `Views/AdminErDiagram/Index.cshtml` serializando as tabelas/relacionamentos do `IAdminDatabaseSchemaService`;
- [x] adicionar assets locais de `React`, `ReactDOM` e `ReactFlow` em `wwwroot/lib/`;
- [x] criar `wwwroot/js/admin-er-diagram.js` com node customizado de tabela, chips de dominio/contexto, foco local, `MiniMap`, `Controls` e `fitView`;
- [x] adicionar auto-layout por dependencias com `dagre` local e botao `Reaplicar auto-layout` na toolbar do canvas;
- [x] criar `wwwroot/css/admin-er-diagram.css` para a shell do modulo e cards do diagrama;
- [x] adicionar o item `Diagramar ER` no menu lateral do Portal Admin;
- [x] adicionar teste `AdminErDiagramControllerTests`;
- [x] atualizar manual QA/Operacao com novo caso `QA-ADM-074`, troubleshooting e historico;
- [x] registrar a entrega no changelog `Released` e no indice de stories `DONE`.
