# ST-003 - Checklist tecnico por categoria de servico

Status: Backlog  
Epic: EPIC-001

## Objetivo

Padronizar execucao tecnica por categoria para aumentar qualidade e reduzir retrabalho, exigindo itens minimos antes da conclusao do atendimento.

## Criterios de aceite

- Admin consegue cadastrar template de checklist por categoria ativa.
- Prestador visualiza checklist do pedido conforme categoria selecionada.
- Itens obrigatorios devem ser marcados para permitir finalizacao.
- Itens podem aceitar evidencias (foto/video) e observacoes.
- Cliente visualiza checklist preenchido no resumo final do servico.
- Historico registra quem marcou/desmarcou e quando.

## Tasks

- [ ] Criar entidades para template de checklist e itens por categoria.
- [ ] Criar entidades para respostas de checklist por atendimento.
- [ ] Criar APIs admin para CRUD de templates de checklist.
- [ ] Criar API prestador para preencher checklist durante atendimento.
- [ ] Implementar validacao de obrigatoriedade antes de concluir servico.
- [ ] Integrar checklist com modulo de anexos/evidencias.
- [ ] Exibir checklist no portal cliente em modo somente leitura.
- [ ] Criar seeds iniciais de checklist para categorias principais.
- [ ] Cobrir regras com testes unitarios e integracao.
- [ ] Atualizar manual de QA com cenarios de checklist obrigatorio.
