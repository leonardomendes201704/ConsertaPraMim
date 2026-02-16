# ST-001 - Cadastro do prestador com wizard, escolha de plano e envio de documentos

Status: Done  
Epic: EPIC-001

## Objetivo

Implementar um wizard de onboarding para novo prestador com 3 planos de assinatura e etapa de upload de documentos de identificacao, com persistencia e validacoes ponta a ponta.

## Criterios de aceite

- Prestador novo visualiza wizard em etapas apos cadastro/login inicial.
- Etapa de plano obriga escolha de 1 entre 3 planos antes de avancar.
- Etapa de documentos permite upload dos arquivos obrigatorios com validacoes.
- Dados do plano e documentos ficam persistidos e consultaveis pelo backend.
- Fluxo suporta salvar progresso e retomar onboarding incompleto.
- Prestador sem onboarding completo nao acessa areas restritas do portal.
- Mensagens de erro/sucesso sao claras e orientam proximo passo.
- Cobertura de testes para regras de negocio e validacoes de upload.

## Tasks

- [x] Definir no dominio os 3 planos de assinatura do prestador (enum/entidade + mapeamento EF).
- [x] Criar estrutura de onboarding do prestador (status, timestamps, flags de concluido por etapa).
- [x] Criar modelo de documento de identificacao (tipo, nome arquivo, mime, tamanho, url, hash opcional, status).
- [x] Implementar migration para novos campos/tabelas de plano e documentos.
- [x] Atualizar seeder para disponibilizar planos padrao em ambiente de dev.
- [x] Criar endpoints API para:
- [x] iniciar onboarding/obter estado atual;
- [x] salvar plano selecionado;
- [x] upload e remocao de documento;
- [x] concluir onboarding.
- [x] Reforcar autorizacao para bloquear acesso a funcionalidades de prestador ate onboarding minimo.
- [x] Implementar validacoes de upload (tipos permitidos, limite de tamanho, limite de quantidade, antivirus hook futuro).
- [x] Implementar sanitizacao de nome de arquivo e storage seguro (sem path traversal).
- [x] Criar wizard no portal do prestador com UX simples:
- [x] etapa 1: dados basicos;
- [x] etapa 2: escolha de plano (3 cards comparativos);
- [x] etapa 3: documentos;
- [x] etapa 4: revisao e conclusao.
- [x] Implementar barra de progresso, botao voltar/avancar e persistencia de estado por etapa.
- [x] Criar pagina/resumo de onboarding pendente apos login para retomar rapidamente.
- [x] Exibir status da assinatura e documentacao no perfil do prestador.
- [x] Criar testes unitarios para regras de selecao de plano e validacao de documentos.
- [x] Criar testes de integracao para endpoints de onboarding e upload.
- [x] Atualizar documentacao tecnica e checklist de QA funcional/seguranca.

## Atualizacao de implementacao (2026-02-13)

- Estrutura de onboarding criada no dominio (`ProviderOnboardingStatus`, `ProviderDocumentType`, `ProviderDocumentStatus`) e entidade `ProviderOnboardingDocument`.
- `ProviderProfile` expandido com status/timestamps de onboarding, plano e relacao com documentos.
- `ProviderOnboardingService` implementado na camada Application para persistencia de etapas, validacoes e conclusao.
- Endpoints API criados em `api/provider-onboarding` com upload seguro (extensao/MIME/tamanho/hash/sanitizacao).
- Wizard multi-step implementado no portal do prestador (`OnboardingController` + view `Views/Onboarding/Index.cshtml`).
- Login/cadastro do prestador agora redireciona automaticamente para onboarding quando pendente.
- Bloqueio de acesso a funcionalidades ate concluir onboarding aplicado no `Program.cs` da API e do portal prestador.
- Migration EF criada: `20260213174325_AddProviderOnboardingWizard`.
- Testes unitarios adicionados para onboarding em:
  - `Backend/tests/ConsertaPraMim.Tests.Unit/Services/ProviderOnboardingServiceTests.cs`
  - `Backend/tests/ConsertaPraMim.Tests.Unit/Controllers/ProviderOnboardingControllerTests.cs`
- Testes de integracao E2E adicionados para onboarding API em:
  - `Backend/tests/ConsertaPraMim.Tests.Unit/Integration/E2E/ProviderOnboardingApiE2ETests.cs`
- Documentacao tecnica e checklist QA/seguranca publicados em:
  - `Documentacao/PRESTADOR_ONBOARDING_ASSINATURA/DOCUMENTACAO_TECNICA_ST-001.md`
  - `Documentacao/PRESTADOR_ONBOARDING_ASSINATURA/CHECKLIST_QA_SEGURANCA_ST-001.md`

## Diagramas

- Fluxo: `Documentacao/DIAGRAMAS/PRESTADOR_ONBOARDING_ASSINATURA/ST-001-cadastro-prestador-wizard-plano-documentos/fluxo-onboarding-api-upload-conclusao.mmd`
- Sequencia: `Documentacao/DIAGRAMAS/PRESTADOR_ONBOARDING_ASSINATURA/ST-001-cadastro-prestador-wizard-plano-documentos/sequencia-onboarding-upload-concluir.mmd`
