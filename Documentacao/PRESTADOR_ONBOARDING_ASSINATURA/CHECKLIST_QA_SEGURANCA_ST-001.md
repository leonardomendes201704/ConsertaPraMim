# Checklist QA Funcional e Seguranca - ST-001

## Objetivo

Validar funcionalmente o onboarding de prestador e verificar controles basicos de seguranca no fluxo de upload.

## Pre-condicoes

- usuario `Provider` autenticado com onboarding pendente;
- API e portal prestador em execucao;
- banco atualizado com migrations vigentes.

## Checklist funcional

- [ ] `GET /api/provider-onboarding` retorna estado do wizard.
- [ ] salvar dados basicos com nome/telefone validos retorna sucesso.
- [ ] salvar plano `Bronze`, `Silver` e `Gold` retorna sucesso.
- [ ] salvar plano `Trial` retorna erro de validacao.
- [ ] upload de `IdentityDocument` com PDF valido retorna sucesso.
- [ ] upload de `SelfieWithDocument` com imagem valida retorna sucesso.
- [ ] endpoint de conclusao falha quando faltar documento obrigatorio.
- [ ] endpoint de conclusao aprova quando criterios completos forem atendidos.
- [ ] prestador sem onboarding completo continua bloqueado em areas restritas.

## Checklist de seguranca (upload)

- [ ] upload com extensao nao permitida (ex.: `.exe`) retorna `400`.
- [ ] upload com MIME nao permitido retorna `400`.
- [ ] upload acima de 10MB retorna `400`.
- [ ] nome de arquivo com path traversal (`../`) e sanitizado.
- [ ] nome de arquivo com caracteres especiais e normalizado/sanitizado.
- [ ] remocao de documento apaga referencia persistida do onboarding.
- [ ] hash SHA-256 e armazenado para documento salvo.

## Evidencias minimas

- payload request/response dos endpoints principais;
- prints do wizard nas etapas de plano e documentos;
- evidencias de cenarios negativos de upload;
- estado final do onboarding marcando concluido;
- trecho de log com `X-Correlation-ID` para ao menos 1 fluxo completo.
