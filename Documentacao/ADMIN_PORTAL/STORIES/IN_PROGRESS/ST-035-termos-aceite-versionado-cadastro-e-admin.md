# ST-035 - Termos de aceite versionados no cadastro (web/mobile) e gestao no admin

Status: In Progress
Epic: EPIC-013

## Objetivo

Implementar termos legais completos (cliente e prestador) com versionamento em banco, aceite obrigatorio no cadastro e area administrativa para manutencao/publicacao das versoes.

## Criterios de aceite

- Existem dois termos independentes (`cliente` e `prestador`) com texto juridico completo e secao explicita de isencao de responsabilidade da plataforma.
- Termos sao versionados no banco e possuem historico de publicacao.
- Cadastro de cliente e prestador (portais e apps) exige aceite do termo ativo para concluir.
- Backend valida aceite e versao enviada no registro; nao depende apenas de checkbox no frontend.
- Aceite fica persistido por usuario com timestamp UTC, publico, versao e origem.
- Portal admin possui menu para visualizar/editar/publicar termos de cliente e prestador.
- Apps/portais consomem termo ativo via API e exibem ao usuario antes do cadastro.

## Tasks

- [x] Task 1 - Planejamento e governanca:
  - Criar EPIC-013 e ST-035 com plano tecnico/juridico.
  - Definir modelo de versionamento e criterios de publicacao.

- [x] Task 2 - Backend dominio e persistencia:
  - Criar entidades para documento de termo versionado e aceite por usuario.
  - Criar migracao EF Core com indices de consulta.
  - Criar seeds iniciais do termo cliente/prestador v1.

- [x] Task 3 - Backend aplicacao e API:
  - Implementar servico de consulta do termo ativo por publico.
  - Implementar API publica para leitura do termo ativo (cliente/prestador).
  - Implementar API admin para listar versoes e publicar nova versao.

- [x] Task 4 - Cadastro com aceite obrigatorio no backend:
  - Estender DTO de cadastro para transportar `termsType`, `termsVersion` e `accepted`.
  - Validar aceite/versao no backend.
  - Persistir aceite na criacao de usuario.

- [x] Task 5 - Portal cliente/prestador:
  - Adicionar bloco de leitura do termo e checkbox obrigatorio.
  - Exibir termo HTML carregado da API.
  - Enviar dados de aceite no cadastro.

- [x] Task 6 - Apps mobile cliente/prestador:
  - Exibir termo ativo antes do cadastro.
  - Exigir aceite explicito para habilitar cadastro.
  - Enviar metadados de aceite na chamada de registro.

- [ ] Task 7 - Portal admin:
  - Adicionar item de menu "Termos Legais".
  - Tela de edicao/publicacao de termos por publico com historico.
  - Validacoes de seguranca para acesso apenas admin.

- [ ] Task 8 - QA, operacao e documentacao:
  - Cobrir fluxo critico com testes unitarios/integracao.
  - Atualizar runbook com procedimento de publicacao de nova versao.
  - Validar E2E web/mobile para cliente e prestador.

## Plano curto

1. Backend primeiro (modelo + API + enforcement) para estabilizar contrato.
2. Portais e apps passam a consumir termo ativo do backend.
3. Admin publica novas versoes sem deploy.
4. Encerrar com testes e runbook de operacao.
