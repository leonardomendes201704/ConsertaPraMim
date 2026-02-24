# Runbook ST-045 - Confianca e verificacao de prestadores

## Objetivo

Padronizar criterios de verificacao, reduzir risco operacional e dar transparencia ao cliente sobre o nivel de confianca do prestador.

## Niveis de risco

- `Baixo`: documentacao obrigatoria aprovada, rating >= 4.0, sem disputa critica aberta nos ultimos 90 dias.
- `Medio`: ate dois alertas operacionais ou rating entre 3.0 e 3.99.
- `Alto`: pendencia documental grave, fraude/conduta grave, disputa critica recente, reincidencia de no-show.

## Status de confianca

- `Pending`: em analise.
- `Verified`: elegivel para selo e destaque.
- `Restricted`: com restricao temporaria ate tratativa.

## SLA operacional

- Primeira analise: ate 24h.
- Reanalise: ate 12h.
- Escalonamento critico: ate 2h.

## Checklist de decisao admin

1. Validar documentos obrigatorios.
2. Revisar historico de cancelamento/no-show.
3. Revisar disputas abertas e severidade.
4. Revisar rating e volume de avaliacoes.
5. Registrar justificativa de decisao.
6. Aplicar status final e notificar prestador.

## Evidencia e auditoria

- Toda decisao deve gerar trilha de auditoria com:
  - status anterior e novo status;
  - risco calculado;
  - motivo operacional;
  - admin responsavel;
  - timestamp UTC.

## Alinhamento com termos legais e comunicacao

1. Sempre que a politica de confianca mudar (criterio, SLA, efeitos de `Restricted`), abrir revisao dos termos legais (`cliente` e `prestador`) no portal admin.
2. Garantir que as clausulas indiquem:
   - carater informativo do selo de confianca;
   - inexistencia de garantia absoluta sobre resultado tecnico;
   - possibilidade de limitacao operacional de conta em risco alto.
3. Publicar nova versao de termos antes de ativar regra nova em producao.
4. Registrar no changelog a data da publicacao e impacto operacional.
