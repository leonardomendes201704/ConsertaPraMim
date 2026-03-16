# ST-105 - Matching geografico e elegibilidade de prestadores

Status: Backlog
Epic: EPIC-JORNADA-001

## Objetivo

Encontrar apenas prestadores realmente elegiveis para cada caso com base em categoria, raio e status operacional.

## Criterios de aceite

- O sistema filtra por categoria e subcategoria.
- O sistema respeita raio de atendimento e localizacao do cliente.
- O sistema considera status operacional e capacidade minima.
- Prestadores fora do recorte nao sao disparados.

## Tasks

- [ ] Revisar modelo de categoria e raio de atendimento do prestador.
- [ ] Implementar consulta geoespacial por coordenada e raio.
- [ ] Adicionar filtros por status, bloqueios e capacidade.
- [ ] Criar ranking dos elegiveis.
- [ ] Registrar trilha de quem foi elegivel e por que.
- [ ] Cobrir regiao sem cobertura e categoria sem oferta suficiente.
