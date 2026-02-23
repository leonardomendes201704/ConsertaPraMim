# EPIC-016 - Wiki de documentacao markdown no Portal Admin

Status: In Progress
Trilha: ADMIN_PORTAL

## Objetivo

Disponibilizar no Portal Admin um visualizador de documentacao em markdown (wiki) para consulta rapida de runbooks, historias, epics e guias tecnicos sem sair do sistema.

## Problema de negocio

- Documentacao esta espalhada em varios arquivos `.md` fora do portal.
- Operacao/QA precisam alternar entre editor/repositorio e portal.
- Falta um ponto unico de consulta com navegacao por categorias e leitura formatada.

## Resultado esperado

- Menu dedicado "Wiki Docs" no Portal Admin.
- Navegacao por estrutura de pastas da documentacao.
- Leitura markdown renderizada com metadados basicos (arquivo, categoria, ultima atualizacao).
- Busca/filtro rapido de arquivos dentro da wiki.

## Metricas de sucesso

- Time operacional encontra documentos criticos em poucos cliques no portal.
- Nao ha necessidade de abrir repositório para leitura de guias operacionais.
- Wiki funciona em desktop e mobile sem quebrar layout.

## Escopo

### Inclui

- Indexacao de arquivos `.md` dentro da pasta `Documentacao`.
- Tela de wiki no portal admin (lista + leitura).
- Renderizacao markdown para HTML no lado servidor.
- Atualizacao do menu lateral.

### Nao inclui

- Edicao de markdown dentro do portal (somente leitura nesta fase).
- Controle de versao via UI.
- Upload de novos arquivos pelo portal.

## Historias vinculadas

- ST-038 - Wiki markdown E2E no Portal Admin.
