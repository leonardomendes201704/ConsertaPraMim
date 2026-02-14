# EPIC-001 - Execucao de campo, status operacional, checklist e evidencias

## Objetivo

Estruturar a fase de atendimento em campo para que cliente, prestador e administracao tenham visibilidade do andamento real do servico, padronizacao tecnica e trilha auditavel de entrega.

## Problema atual

- O pedido passa para "agendado", mas falta telemetria da execucao real.
- Nao existe marco formal de chegada/inicio/fim do atendimento.
- A qualidade da execucao depende apenas de texto livre no chat.
- Evidencias tecnicas e comprovacao de entrega ficam dispersas.

## Resultado esperado

- Prestador faz check-in ao chegar e inicio efetivo do atendimento.
- Pedido passa por estados operacionais claros em tempo real.
- Cada categoria possui checklist minimo de qualidade.
- Evidencias de antes/depois ficam vinculadas ao pedido e historico.
- Conclusao formal com assinatura/PIN reduz contestacoes.

## Escopo

### Inclui

- Check-in geolocalizado com timestamp.
- Estados operacionais de campo.
- Checklist tecnico por categoria.
- Upload e gestao de evidencias operacionais.
- Encerramento formal do atendimento.

### Nao inclui

- Roteirizacao multi-visita no mesmo turno.
- App mobile offline-first completo.
- Assinatura digital ICP-Brasil.

## Metricas de sucesso

- >= 90% dos atendimentos com check-in registrado.
- >= 80% dos atendimentos com checklist completo.
- Reducao de disputas por "servico nao realizado" em >= 30%.
- >= 85% dos pedidos concluidos com evidencias anexadas.

## Historias vinculadas

- ST-001 - Check-in de chegada e inicio de atendimento.
- ST-002 - Status operacional do atendimento em tempo real.
- ST-003 - Checklist tecnico por categoria de servico.
- ST-004 - Evidencias de execucao (antes/depois) vinculadas ao pedido.
- ST-005 - Finalizacao formal com resumo e assinatura digital/PIN.
