# ST-023 - UI de suporte no portal prestador

Status: Backlog  
Epic: EPIC-008

## Objetivo

Como prestador, quero uma area de suporte com lista de chamados e conversa para abrir e acompanhar atendimentos.

## Criterios de aceite

- Existe item de menu de suporte no portal prestador.
- Tela lista chamados com status, prioridade, ultima atualizacao e acao de detalhe.
- Prestador consegue abrir novo chamado via formulario dedicado.
- Tela de detalhe exibe historico e permite enviar nova mensagem.
- Fluxo de erro, carregamento e vazio tratados de forma clara.

## Tasks

- [ ] Criar menu e rota de suporte no `ConsertaPraMim.Web.Provider`.
- [ ] Implementar tela de listagem de chamados com paginacao.
- [ ] Implementar tela/modal de criacao de chamado.
- [ ] Implementar tela de detalhe com timeline de mensagens.
- [ ] Integrar com endpoints provider de ST-021.
- [ ] Aplicar padrao visual e responsividade para desktop/mobile web.
- [ ] Adicionar estados UX (loading, empty state, erro de API).
- [ ] Criar testes funcionais basicos da navegacao e do fluxo principal.
- [ ] Criar/atualizar diagrama de fluxo Mermaid da funcionalidade.
- [ ] Criar/atualizar diagrama de sequencia Mermaid da funcionalidade.
