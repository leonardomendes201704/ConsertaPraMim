# ST-042 - Score de liquidez por regiao/categoria e alertas de deficit

Status: Done
Epic: EPIC-018

## Objetivo

Calcular score de liquidez por regiao/categoria para orientar aquisicao de prestadores e reduzir pedidos sem proposta.

## Criterios de aceite

- Score de liquidez calculado por regiao/categoria em janela configuravel.
- Classificacao em faixas (critico, atencao, saudavel).
- Alertas quando score cair abaixo de limiar.
- Exportacao para operacao/comercial.

## Tasks

- [x] Definir formula de liquidez (demanda x oferta x tempo de resposta).
- [x] Implementar servico de calculo e armazenamento agregado.
- [x] Expor endpoint de score e historico.
- [x] Criar visual no portal admin (mapa/lista priorizada).
- [x] Documentar playbook de acao por faixa de score.
