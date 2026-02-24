# ST-045 - Camadas de confianca e verificacao de prestadores

Status: In Progress
Epic: EPIC-018

## Objetivo

Reforcar confianca na plataforma com verificacao de prestador, selo de confianca e transparencia para o cliente.

## Criterios de aceite

- Prestador possui status de verificacao visivel (pendente, verificado, restrito).
- Cliente visualiza selo e criterios basicos de verificacao.
- Fluxo de auditoria para aprovar/reprovar verificacao.
- Regras de bloqueio em caso de risco operacional.

## Politica de verificacao por nivel de risco (v1)

- `Nivel baixo`: prestador com documentacao basica aprovada, sem alertas recentes de conduta/no-show, rating >= 4.0 e sem disputas criticas abertas nos ultimos 90 dias.
- `Nivel medio`: prestador com documentacao completa, mas com 1-2 alertas operacionais (cancelamento tardio, SLA recorrente, disputa moderada) ou rating entre 3.0 e 3.99.
- `Nivel alto`: prestador com pendencia documental relevante, disputa critica recente, reincidencia de no-show/cancelamento tardio ou evidencias de conduta inadequada.

### Status de confianca

- `Pending`: onboarding ou revisao complementar em andamento.
- `Verified`: prestador elegivel para destaque no ranking e selo visivel para cliente.
- `Restricted`: prestador com limitacao operacional temporaria ate saneamento das pendencias.

### Regras de transicao

- `Pending -> Verified`: todos os documentos obrigatorios aprovados + score de risco abaixo do limite + sem bloqueios legais.
- `Pending -> Restricted`: identificacao de risco alto ou inconsistencias criticas.
- `Verified -> Restricted`: evento critico novo (fraude, conduta grave, reincidencia de no-show/cancelamento fora de politica).
- `Restricted -> Pending`: pendencia tratada, aguardando reavaliacao final pelo time admin.

### SLA operacional

- Primeira analise: ate 24h apos envio documental.
- Reanalise pos-correcoes: ate 12h.
- Escalonamento de caso critico: imediato (ate 2h).

## Tasks

- [x] Definir politica de verificacao por nivel de risco.
- [x] Criar entidade/processo de verificacao com trilha de auditoria.
- [x] Expor status no perfil do prestador e card de proposta.
- [x] Implementar fila admin para analise e decisao.
- [ ] Atualizar termos/politicas operacionais conforme regra.
