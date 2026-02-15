# ST-004 - Evidencias de execucao (antes/depois) vinculadas ao pedido

Status: In Progress  
Epic: EPIC-001

## Objetivo

Fortalecer comprovacao de execucao por meio de midias estruturadas de antes/depois, vinculadas ao atendimento e reutilizaveis na galeria do prestador.

## Criterios de aceite

- Prestador pode anexar evidencias com tipo `ANTES`, `DURANTE`, `DEPOIS`.
- Cada evidencia fica vinculada ao `ServiceRequestId` e `AppointmentId`.
- Cliente visualiza galeria de evidencias no detalhe do pedido.
- Admin pode visualizar e moderar evidencias em caso de disputa.
- Upload respeita limites de tamanho, formato e antivirus/politicas.
- Metadados de autoria e horario sao persistidos.

## Tasks

- [x] Evoluir modelo de midias para suportar fase operacional da evidencia.
- [x] Criar endpoint de upload especifico para evidencias de atendimento.
- [x] Implementar validacao de MIME, extensao, tamanho e scan basico.
- [x] Adicionar compressao/thumbnail para imagens e preview de videos.
- [x] Integrar evidencias com galeria existente por album de servico.
- [x] Exibir evidencias na tela do cliente com ordenacao temporal.
- [x] Exibir evidencias no portal admin com filtro por pedido e fase.
- [ ] Garantir autorizacao por papel e ownership dos arquivos.
- [ ] Criar testes de seguranca para upload malicioso.
- [ ] Definir politica de retencao e limpeza de evidencias antigas.
