# EPIC-005 - Manual operacional e de QA do Portal Admin

Status: In Progress

## Objetivo

Disponibilizar um manual completo em HTML, acessivel dentro do Portal Admin, para apoiar operacao, QA funcional e onboarding de novos operadores.

## Problema atual

- Nao existe um guia unico e consolidado com todas as funcionalidades do Portal Admin.
- Validacoes de QA dependem de conhecimento tacito e informacoes espalhadas.
- Nao ha regra formal para manter documentacao operacional sincronizada com mudancas de produto.

## Resultado esperado

- Portal Admin passa a ter item de menu dedicado ao manual.
- Manual cobre 100% das funcionalidades administrativas existentes.
- Manual descreve uso, operacao, validacao QA e resultados esperados por fluxo.
- Processo de atualizacao do manual vira regra obrigatoria em qualquer alteracao funcional do Admin.

## Metricas de sucesso

- 100% das telas/fluxos do Admin com cobertura no manual.
- Tempo medio de onboarding de QA para Portal Admin < 2h.
- Reducao de duvidas operacionais recorrentes em rotina de suporte interno.
- 0 entregas de funcionalidades Admin sem atualizacao correspondente no manual.

## Escopo

### Inclui

- Criacao de pagina HTML completa do manual dentro do `ConsertaPraMim.Web.Admin`.
- Estrutura por modulos: dashboard, usuarios, pedidos, propostas, chats, categorias, planos/ofertas.
- Casos de uso e historias em linguagem de usuario.
- Casos de teste funcionais (smoke/regressao) com resultado esperado.
- Checklist operacional diario/semanal e troubleshooting basico.
- Regra de governanca: alteracoes de Admin exigem atualizacao do manual.

### Nao inclui

- Automacao de testes E2E.
- Manual de API publica para terceiros.
- Manual de operacao de infraestrutura/cloud.

## Historias vinculadas

- ST-014 - Criar manual HTML completo do Portal Admin, publicar no menu e instituir politica de atualizacao obrigatoria.
