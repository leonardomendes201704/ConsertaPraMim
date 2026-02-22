# RUNBOOK - Termos Legais Versionados (ST-035)

## Objetivo

Padronizar a operacao de manutencao e publicacao dos termos legais de cadastro (`cliente` e `prestador`) no Portal Admin, garantindo rastreabilidade e rollout seguro sem deploy.

## Escopo

- Leitura do termo ativo por publico.
- Publicacao de nova versao de termo.
- Validacao pos-publicacao em web e mobile.
- Procedimento de rollback funcional (republicacao de versao anterior).

## Pre-requisitos

- Usuario logado com perfil `Admin`.
- API admin online e autenticacao operacional.
- Termo em HTML validado pelo time juridico/compliance.

## Acesso no Portal Admin

1. Entrar no Portal Admin.
2. Menu lateral: `Termos Legais`.
3. Selecionar aba/pill do publico:
   - `Cliente`
   - `Prestador`

## Publicacao de nova versao

1. Confirmar no painel direito qual e a versao ativa atual (`vN`).
2. No card `Publicar nova versao`, preencher:
   - `Titulo do termo`
   - `Resumo da alteracao` (opcional, recomendado)
   - `Conteudo HTML`
3. Acionar `Publicar nova versao`.
4. Validar feedback de sucesso:
   - mensagem de confirmacao com a nova versao (`vN+1`);
   - historico atualizado no grid;
   - termo ativo atualizado com `Publicado`.

## Rollback funcional

Nao existe botao de "voltar versao" direto. O rollback e feito por republicacao:

1. Copiar o HTML e titulo da versao anterior desejada (historico).
2. Publicar novamente esse conteudo como nova versao (`vN+1`).
3. Registrar no resumo da alteracao: `Rollback funcional para versao vX`.

## Validacao pos-publicacao (E2E minimo)

### Web Cliente

1. Abrir tela de cadastro no portal cliente.
2. Confirmar exibicao do novo termo ativo.
3. Validar que cadastro sem aceite e bloqueado.
4. Validar que cadastro com aceite conclui.

### Web Prestador

1. Abrir tela de cadastro no portal prestador.
2. Confirmar exibicao do novo termo ativo.
3. Validar que cadastro sem aceite e bloqueado.
4. Validar que cadastro com aceite conclui.

### Mobile Cliente / Prestador

1. Abrir fluxo `Criar conta` no app.
2. Confirmar carregamento do termo ativo.
3. Validar bloqueio quando termo nao carregado/nao aceito.
4. Validar envio com sucesso apos aceite.

## Evidencias recomendadas

- Print da tela `Termos Legais` com nova versao ativa.
- Print do historico com timestamp de publicacao.
- Print dos fluxos de cadastro web/mobile com termo exibido.

## Troubleshooting

### Erro de permissao no Portal Admin

- Verificar se usuario possui role `Admin`.
- Reautenticar no portal para renovar token.

### Falha ao salvar/publicar

- Verificar conectividade da API.
- Validar campos obrigatorios (`Titulo` e `Conteudo HTML`).
- Conferir logs da API para `legal_terms_*`.

### Termo nao aparece no cadastro

- Confirmar publico correto (`cliente` x `prestador`).
- Confirmar se a publicacao terminou com sucesso.
- Limpar cache da pagina/app e recarregar.

