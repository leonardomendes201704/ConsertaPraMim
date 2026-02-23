# AGENTS.md

## Escopo

Estas diretrizes valem para todo o repositorio `ConsertaPraMimWeb`.

## Diretriz obrigatoria de changelog

1. Toda mudanca que altere comportamento funcional, fluxo de negocio, API, UI, configuracao operacional, deploy ou testes deve registrar entrada no changelog.
2. O changelog oficial da solution fica em:
   - `Documentacao/ADMIN_PORTAL/CHANGELOG/CHANGELOG.md`
3. A entrada deve ser registrada seguindo o template ja definido no arquivo:
   - data + story/identificador
   - tipo (`feat|fix|refactor|docs|test`)
   - resumo objetivo
   - arquivos principais
   - risco/impacto
4. Antes de qualquer `commit`/`push` (incluindo em `dev-local`), a entrada da entrega deve estar marcada em `## Released`.
5. `Unreleased` pode ser usado apenas como area temporaria de trabalho durante a implementacao local, nunca como estado final apos `commit`/`push`.

## Diretriz obrigatoria de manual QA/Operacao

1. Toda feature nova ou alteracao funcional, em qualquer projeto do repositorio (`Backend`, `Web`, `Mobile`, `scripts`, `deploy/infra`), deve criar ou atualizar manual de QA/Operacao no mesmo ciclo de entrega.
2. Se ja existir manual para a trilha/projeto impactado, ele deve ser editado com os novos fluxos, regras, validacoes, casos de teste e troubleshooting.
3. Se nao existir manual para a trilha/projeto impactado, um novo manual deve ser criado e referenciado no indice/documentacao da trilha.
4. A atualizacao/criacao do manual deve ser versionada no mesmo commit/PR da feature correspondente.
5. Sem manual atualizado/criado (quando aplicavel), a story nao pode ser considerada concluida.

## Diretrizes operacionais de entrega

1. Feature que impacta mais de um modulo deve abrir/atualizar Epic + Story + Tasks antes da implementacao.
2. A implementacao deve seguir em passos curtos, com commit/push por task concluida.
3. Antes de cada commit, executar build/testes minimos dos projetos impactados.
4. Toda mudanca funcional deve atualizar CHANGELOG, manual QA/Operacao e status da Story.
5. Mudanca de fluxo, regra de negocio ou contrato de API deve atualizar diagramas Mermaid e indices de documentacao no mesmo ciclo.
6. Fluxo Git padrao: trabalhar em `dev-local`, abrir PR para `main` e evitar commit direto em `main` sem solicitacao explicita.
7. Deploy/pipeline deve manter grafo legivel por etapa critica e resumo com status claro do que foi ou nao executado.
8. Datas/horarios devem ser persistidos em UTC e exibidos no fuso de negocio (`America/Sao_Paulo`) quando aplicavel.
9. Segredos nao podem ser commitados; usar env/secrets e validar carregamento em runtime.
10. Mudanca em app mobile deve incrementar versao/build e garantir publicacao de APK no fluxo de deploy.
11. Correcao de bug relevante deve incluir teste de regressao.
12. Encerramento de demanda deve ter resumo tecnico objetivo e validacoes executadas; quando houver mecanismo ativo, enviar notificacao de conclusao.

## Excecoes (quando NAO precisa registrar)

1. Ajuste puramente local de desenvolvimento sem impacto no repositorio final.
2. Mudanca exclusivamente cosmetica sem impacto funcional (ex.: espacos, formatacao automatica).

## Definicao de pronto (DoD)

Uma tarefa so deve ser considerada concluida quando:

1. codigo estiver implementado;
2. validacao/build/testes aplicaveis tiverem sido executados;
3. changelog estiver registrado e marcado como `Released` antes de commit/push (quando aplicavel);
4. manual de QA/Operacao da trilha/projeto estiver criado/atualizado (quando aplicavel).
