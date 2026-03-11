# AGENTS.md

## Escopo

Estas diretrizes valem para todo o repositorio `ConsertaPraMimWeb`.

## Diretriz obrigatoria de mapeamento fixo de projetos (Portal/API)

1. Toda solicitacao que citar `Portal Admin` deve ser direcionada diretamente para `Backend/src/ConsertaPraMim.Web.Admin`.
2. Toda solicitacao que citar `Portal do Cliente` deve ser direcionada diretamente para `Backend/src/ConsertaPraMim.Web.Client`.
3. Toda solicitacao que citar `Portal do Prestador` deve ser direcionada diretamente para `Backend/src/ConsertaPraMim.Web.Provider`.
4. Toda solicitacao que citar `API` (ou `Api`) deve ser direcionada diretamente para `Backend/src/ConsertaPraMim.API`.
5. Para os quatro casos acima, o agente nao deve iniciar com busca ampla no repositorio; o ponto de partida obrigatorio e o caminho mapeado.
6. Quando a demanda mencionar explicitamente `app mobile`:
   - Admin: `conserta-pra-mim-admin app`
   - Cliente: `conserta-pra-mim app`
   - Prestador: `conserta-pra-mim-provider app`
7. Se houver ambiguidade entre `portal web` e `app mobile`, a confirmacao com o solicitante deve ocorrer antes de qualquer alteracao.

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

## Diretriz obrigatoria de filtros em telas

1. Toda tela nova ou alterada que possua filtros de consulta deve usar o mesmo padrao visual/comportamental: `offcanvas/drawer`.
2. O acesso aos filtros deve ocorrer por botao explicito (`Filtros`) no cabecalho/toolbar da tela.
3. Formularios extensos de filtro nao devem ficar inline no corpo principal; excecao precisa ser justificada no changelog e no manual QA/Operacao.
4. O drawer deve manter acoes minimas de `Aplicar filtros` e `Limpar filtros`, preservando consistencia entre modulos.

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

## Diretriz obrigatoria de idioma e exibicao no front

1. Todo texto exibido ao usuario em qualquer front (`Web`, `Mobile`, `Portal Admin`, `Portal Cliente`, `Portal Prestador`) deve estar em PT-BR, salvo quando houver exigencia explicita de integracao tecnica ou marca oficial.
2. Nenhuma tela deve expor valores crus de enum, status interno, nome de classe ou codigo tecnico diretamente ao usuario final.
3. Enums usados em UI devem possuir `DisplayName`/rotulo equivalente em PT-BR e a renderizacao do front deve consumir esse rotulo, nunca o valor tecnico bruto.
4. Ao criar novo status, enum ou campo exibivel, a traducao PT-BR deve ser entregue no mesmo ciclo da implementacao.
5. Encontrar texto tecnico/ingles exposto em tela deve ser tratado como bug de UX/release e corrigido no mesmo fluxo da demanda.

## Diretriz obrigatoria de resumo de commit por task

1. Ao concluir qualquer solicitacao com implementacao (mesmo sem commit imediato), a resposta final deve incluir no encerramento um bloco em Markdown com texto de commit detalhado por task entregue.
2. O bloco deve seguir padrao minimo:
   - `Titulo sugerido do commit`
   - `Tipo` (`feat|fix|refactor|docs|test|chore`)
   - `Contexto/objetivo`
   - `Arquivos principais alterados`
   - `Validacoes executadas` (build/testes/comandos)
   - `Risco/impacto`
3. Quando houver mais de uma task na mesma solicitacao, deve haver um bloco de commit separado para cada task concluida, na ordem de entrega.
4. O formato deve ser legivel em Markdown e ficar no final da resposta de conclusao.

## Diretriz obrigatoria de CSP e assets externos

1. Qualquer tela/feature que adicione CSS/JS/imagens/fontes de origem externa deve validar previamente compatibilidade com `Content-Security-Policy` do projeto alvo.
2. Nao e permitido referenciar CDN/origem sem garantir que a origem esteja liberada no CSP (`script-src`, `style-src`, `img-src`, `font-src`, `connect-src`, conforme o tipo do recurso).
3. Antes de concluir a task, executar verificacao funcional minima no browser (ou checklist tecnico equivalente) para confirmar que nao houve bloqueio de recurso por CSP.
4. Preferir fontes ja homologadas no projeto (ex.: `cdnjs`) ou assets locais versionados quando houver risco de bloqueio por politica.
5. Mudanca de CSP deve ser registrada explicitamente no changelog e no manual QA/Operacao quando impactar carregamento de telas.

## Diretriz obrigatoria de encoding

1. Todo arquivo textual versionado do repositorio (`.cs`, `.cshtml`, `.js`, `.json`, `.md`, `.yml`, etc.) deve ser salvo em `UTF-8`.
2. Nao e permitido salvar arquivos-fonte em `ANSI`, `Windows-1252`, `ISO-8859-*` ou qualquer encoding local dependente do sistema operacional.
3. Ao corrigir textos acentuados, validar o encoding real do arquivo em bytes; nao basta o editor exibir o texto aparentemente correto.
4. Antes de concluir uma task que altere textos/UI/documentacao, executar verificacao minima para garantir que nao houve regressao de caracteres quebrados (ex.: replacement-char, A-tilde, A-circumflex) no ambiente publicado.
5. Toda task que altere arquivos textuais deve executar varredura no modulo impactado por caracteres quebrados (ex.: replacement-char, A-tilde, A-circumflex) antes do encerramento, corrigindo qualquer ocorrencia no mesmo ciclo.
6. Arquivo textual corrigido por copy/acentuacao deve ser regravado explicitamente em `UTF-8` e validado novamente apos a escrita; `UTF-8` sem BOM e o padrao preferencial para fontes web.
7. O repositorio deve manter configuracao de editor para forcar `UTF-8` (ex.: `.editorconfig`), e qualquer desvio deve ser tratado como bug de release.

## Diretriz obrigatoria de documentacao Swagger/OpenAPI

1. Endpoint novo ou alterado em `ConsertaPraMim.API` deve sair no mesmo ciclo com documentacao Swagger atualizada, com contexto de negocio e tecnico do ecossistema ConsertaPraMim, sem texto generico.
2. A documentacao precisa manter paridade entre:
   - `Backend/src/ConsertaPraMim.API/Swagger/ApiEndpointDocumentationCatalog.cs`
   - `Backend/src/ConsertaPraMim.API/Swagger/ComprehensiveSwaggerOperationFilter.cs`
   - `Backend/src/ConsertaPraMim.API/Swagger/ApiTagDescriptionsDocumentFilter.cs`
3. Endpoint sem narrativa de negocio, parametros relevantes, respostas esperadas e implicacoes operacionais nao atende DoD.
4. Sempre que houver novo controller/acao/rota:
   - mapear o dominio/tag no catalogo;
   - revisar descricao da tag no documento OpenAPI;
   - garantir exemplos de chamada (cURL/request model) quando aplicavel.
5. Se endpoint for interno e nao puder aparecer no Swagger, a decisao deve ser explicita com `ApiExplorerSettings(IgnoreApi = true)` e registrada no changelog/manual.
6. Mudanca de contrato (request/response/status code/autorizacao) exige atualizacao sincronizada de Swagger + manual QA/Operacao + changelog.
7. Falha de documentacao Swagger em endpoint exposto deve ser tratada como bug de release.

## Excecoes (quando NAO precisa registrar)

1. Ajuste puramente local de desenvolvimento sem impacto no repositorio final.
2. Mudanca exclusivamente cosmetica sem impacto funcional (ex.: espacos, formatacao automatica).

## Definicao de pronto (DoD)

Uma tarefa so deve ser considerada concluida quando:

1. codigo estiver implementado;
2. validacao/build/testes aplicaveis tiverem sido executados;
3. changelog estiver registrado e marcado como `Released` antes de commit/push (quando aplicavel);
4. manual de QA/Operacao da trilha/projeto estiver criado/atualizado (quando aplicavel).

## Diretriz obrigatoria de ambiente local Windows (PowerShell, Node e Git)

1. Em projetos Node/Vite/Capacitor do repositorio, no Windows, nao usar `npm` direto no PowerShell quando houver risco de bloqueio por `ExecutionPolicy`; o padrao obrigatorio e executar via `cmd.exe /c npm ...` ou `npm.cmd ...`.
2. `PowerShell` local nao deve usar `&&` como padrao operacional do repositorio. Sequencias de comandos devem ser:
   - separadas em comandos independentes; ou
   - encadeadas com `;` quando seguro; ou
   - encapsuladas explicitamente em `cmd.exe /c "comando1 && comando2"` quando realmente necessario.
3. Se `vite build`, `npm run build` ou `esbuild` falharem com `spawn EPERM`, o procedimento obrigatorio e:
   - encerrar processos Node/Vite/preview/dev ainda abertos do mesmo app;
   - executar novo build a partir de shell limpo;
   - preferir `cmd.exe /c npm run build` para evitar o caminho `npm.ps1` do PowerShell;
   - evitar rodar build, dev server e preview simultaneamente no mesmo app quando houver lock de arquivo no Windows.
4. Se houver falha recorrente de `spawn EPERM`, tratar como problema de lock/antivirus/processo do ambiente local Windows, nao como erro automatico de codigo. Nesses casos, a resposta operacional padrao deve orientar rebuild limpo e reinicio do processo local antes de qualquer refatoracao desnecessaria.
5. Quando for necessario validar build publicado localmente, preferir a sequencia:
   - `cmd.exe /c npm run build`
   - `cmd.exe /c npx vite preview --host 127.0.0.1 --port <porta>`
   em vez de manter instancias antigas que podem servir bundle stale.
6. Se Git falhar com `Unable to create .git/index.lock`, `Permission denied` ou erro equivalente de escrita em `.git`, nao repetir `git add`/`commit`/`push` em loop. O procedimento obrigatorio e:
   - verificar se existe `index.lock` residual;
   - verificar se outro processo/editor/antivirus esta segurando `.git`;
   - confirmar se o shell atual possui permissao real de escrita em `.git`;
   - se a escrita continuar negada, interromper a tentativa e reportar o bloqueio operacional ao usuario com os comandos exatos para execucao manual.
7. Em ambiente Windows deste repositorio, `git add`, `git commit` e `git push` devem ser executados como passos separados quando houver qualquer suspeita de restricao no shell, evitando comandos compostos que escondam a etapa que falhou.
8. Arquivos locais/transientes de diagnostico, como `Backend/src/debug.log`, nunca devem ser incluidos em commit por causa de workaround operacional de build ou de shell.
9. Toda vez que uma task depender de build Node no Windows, a resposta final deve registrar explicitamente qual comando foi considerado o caminho estavel de execucao local (`cmd.exe /c npm ...`, `npm.cmd ...`, preview rebuildado, etc.), para reduzir reincidencia do mesmo erro.
10. Operacoes com mais de um passo (ex.: kill de porta + build + start + healthcheck) nao podem ser executadas em comando unico monolitico no terminal automatizado. O padrao obrigatorio e executar em etapas separadas e observaveis.
11. Em caso de bloqueio por politica do ambiente ao executar uma operacao composta, o fallback obrigatorio e:
   - quebrar em comandos atomicos;
   - validar cada etapa por codigo de saida/resultado;
   - somente avancar para a proxima etapa apos sucesso explicito da anterior.
12. Para fluxos recorrentes de operacao local (ex.: restart de app em porta fixa), preferir script versionado em `scripts/` e chamar o script diretamente, em vez de one-liners longos.
13. Em Windows, e proibido usar script inline por heredoc/redirecionamento (ex.: `python - <<`, here-doc bash emulado, ou bloco multiline embutido no comando) para tarefas que alterem arquivos ou assets do repositorio.
14. Se a politica do shell bloquear script inline, o procedimento obrigatorio e:
   - criar script `.py` ou `.ps1` explicito em caminho previsivel (`scripts/` para rotina recorrente, ou `scripts/_tmp/` para rotina pontual);
   - executar o script por arquivo (`python scripts/...` / `powershell -File scripts/...`);
   - validar o resultado e remover o arquivo temporario ao final quando for rotina pontual.
15. `python -c` deve ser usado apenas para comandos curtos de leitura/diagnostico (one-liner simples). Nao usar `python -c` longo/obfuscado para substituir script bloqueado por politica.
16. Sempre que houver bloqueio de politica, registrar na resposta final qual foi o fallback aplicado (arquivo de script usado e comando de execucao), para manter rastreabilidade e evitar repeticao de tentativa inadequada.

## Diretriz obrigatoria de escrita deterministica de arquivos no shell

1. Para alterar arquivo textual versionado (`.md`, `.cs`, `.cshtml`, `.js`, `.json`, `.yml`), priorizar `apply_patch` com contexto explicito; nao usar comando de append como fallback quando a insercao no ponto esperado falhar.
2. Nao usar pipeline aninhada de PowerShell para escrita (ex.: `... | powershell -Command -` dentro de outro `powershell`). Em caso de comando shell necessario, executar um unico processo PowerShell por etapa.
3. Quando o conteudo tiver crase/backtick, aspas ou markdown sensivel, usar here-string literal (`@' ... '@`) para evitar escape/interpolacao acidental do PowerShell.
4. Se a insercao contextual falhar, interromper a escrita e validar o alvo antes de tentar novamente:
   - localizar marcador com `rg`/`Select-String`;
   - revisar diff parcial do arquivo;
   - reaplicar patch com contexto correto.
5. Apos qualquer escrita por shell, validar obrigatoriamente no mesmo ciclo:
   - conteudo no ponto esperado;
   - ausencia de caracteres quebrados;
   - diff restrito ao trecho intencional.
6. E proibido concluir tarefa com mensagem de "append no final para garantir"; esse comportamento e considerado falha de processo e deve ser corrigido antes do encerramento.
