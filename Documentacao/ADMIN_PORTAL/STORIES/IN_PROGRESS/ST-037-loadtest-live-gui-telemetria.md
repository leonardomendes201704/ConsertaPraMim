# ST-037 - Load test com GUI live e telemetria em tempo real

Status: In Progress
Epic: EPIC-015

## Objetivo

Entregar uma interface grafica para executar cenarios de carga e acompanhar telemetria em tempo real (progresso, volume, latencia e erros) durante a execucao.

## Criterios de aceite

- Runner suporta emissao de snapshot live em arquivo JSON durante todo o run.
- Snapshot live contem progresso, contadores, RPS, latencias p50/p95/p99, status codes e top endpoints.
- GUI permite iniciar run com parametros basicos (cenario/base URL/overrides) sem editar scripts manualmente.
- GUI mostra estado do processo (idle/running/success/error) e tempo de execucao.
- GUI atualiza em tempo real com graficos e tabelas sem reiniciar execucao.
- Ao finalizar, GUI exibe caminhos dos relatorios gerados.
- Fluxo atual de relatorios e `--publish-admin` permanece intacto.
- README documenta setup e comandos de execucao GUI.

## Tasks

- [x] Criar Epic/Story da Fase 1 com escopo e criterios de aceite.
- [x] Implementar snapshot live no `loadtest_runner.py`.
- [x] Criar GUI Streamlit para start + monitor em tempo real.
- [ ] Criar scripts de execucao GUI (Windows) sem quebrar scripts atuais.
- [ ] Atualizar `requirements.txt` e `README.md` com passo a passo.
- [ ] Validar execucao local da GUI e registrar evidencias tecnicas.
