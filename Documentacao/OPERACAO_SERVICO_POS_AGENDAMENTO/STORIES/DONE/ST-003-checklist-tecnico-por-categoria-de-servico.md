# ST-003 - Checklist tecnico por categoria de servico

Status: Done  
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

- [x] Criar entidades para template de checklist e itens por categoria.
- [x] Criar entidades para respostas de checklist por atendimento.
- [x] Criar APIs admin para CRUD de templates de checklist.
- [x] Criar API prestador para preencher checklist durante atendimento.
- [x] Implementar validacao de obrigatoriedade antes de concluir servico.
- [x] Integrar checklist com modulo de anexos/evidencias.
- [x] Exibir checklist no portal cliente em modo somente leitura.
- [x] Criar seeds iniciais de checklist para categorias principais.
- [x] Cobrir regras com testes unitarios e integracao.
- [x] Atualizar manual de QA com cenarios de checklist obrigatorio.

## Cenarios QA (Checklist Obrigatorio)

1. Como admin, criar template com 3 itens (2 obrigatorios) para categoria ativa.
2. Como prestador com atendimento em andamento, preencher apenas 1 item obrigatorio e tentar concluir atendimento.
Resultado esperado: API retorna conflito com `required_checklist_pending` e atendimento nao vai para concluido.
3. Como prestador, concluir todos os itens obrigatorios (incluindo evidencia nos itens que exigem arquivo).
Resultado esperado: status operacional permite `Concluido`, agendamento e pedido mudam para concluido.
4. Como cliente, abrir detalhe do pedido e validar bloco de checklist em modo somente leitura.
Resultado esperado: itens, badges de obrigatorio/evidencia e historico recente exibidos corretamente.
5. Como admin, abrir tela `Checklists Tecnicos` e editar template.
Resultado esperado: alteracoes refletidas no proximo atendimento da categoria.
