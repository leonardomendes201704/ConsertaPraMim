# EPIC-015 - Load test com GUI live e telemetria operacional

Status: In Progress
Trilha: ADMIN_PORTAL

## Objetivo

Evoluir o pacote de teste de carga para uma experiencia guiada com acompanhamento em tempo real da execucao, removendo a dependencia exclusiva de logs em terminal.

## Problema de negocio

- Execucao atual via cmd/powershell deixa o operador sem visibilidade instantanea.
- Analise de falhas e gargalos depende de esperar o fim do teste.
- Nao ha painel live consolidado de progresso, throughput, latencia e erros.

## Resultado esperado

- Dashboard local com execucao assistida (GUI) para iniciar e acompanhar cenarios.
- Telemetria live (progress, requests, RPS, p95/p99, erros por status e endpoint).
- Persistencia de snapshot em JSON para debug e integracoes futuras.
- Compatibilidade com fluxo atual de relatorios (json/txt/html) e publicacao admin.

## Metricas de sucesso

- Operador consegue acompanhar run em tempo real sem depender apenas de terminal.
- Snapshot live atualizado em janela curta (1-2 segundos).
- Encerramento do teste exibe status final e caminhos de relatorios.
- Fluxo atual de execucao por linha de comando permanece funcional.

## Escopo

### Inclui

- Snapshot live no runner de loadtest.
- GUI em Streamlit para start/monitor da execucao.
- Scripts auxiliares para subir a GUI no Windows.
- Atualizacao de README e dependencias Python.

### Nao inclui

- Historico centralizado multi-runs em banco.
- Alertas externos (Teams/Slack/Email) a partir da GUI.
- Distribuicao multi-node de carga.

## Historias vinculadas

- ST-037 - Load test com GUI live e telemetria em tempo real.
