# ST-038 - Wiki markdown E2E no Portal Admin

Status: In Progress
Epic: EPIC-016

## Objetivo

Entregar uma area "Wiki Docs" no Portal Admin para navegar e ler arquivos markdown da pasta `Documentacao`, com filtro rapido e leitura formatada.

## Criterios de aceite

- Existe item de menu "Wiki Docs" no sidebar do portal admin.
- Tela de wiki lista documentos markdown por categoria/pasta.
- Leitura do documento selecionado e renderizada em HTML.
- Exibe metadados do documento (caminho relativo e ultima atualizacao).
- Busca/filtro local reduz lista de arquivos em tempo real.
- Nao permite path traversal nem leitura fora da raiz configurada.
- Build do `ConsertaPraMim.Web.Admin` passa sem erros.

## Tasks

- [x] Criar Epic/Story e atualizar board (`INDEX.md`).
- [x] Implementar servico de indexacao e leitura markdown no `ConsertaPraMim.Web.Admin`.
- [x] Criar controller e view da Wiki com sidebar + painel de leitura.
- [x] Integrar menu lateral com item "Wiki Docs".
- [ ] Validar build e registrar evidencias tecnicas.

## Validacao tecnica

Data: 23/02/2026

- Pendente.
