# Documentacao Tecnica - ST-001 (Onboarding de Prestador)

## Escopo tecnico

A ST-001 consolidou o onboarding de prestador com wizard, selecao de plano e upload de documentos, incluindo bloqueio de acesso para prestador incompleto.

## Componentes principais

- API:
  - `ConsertaPraMim.API/Controllers/ProviderOnboardingController.cs`
  - rotas em `api/provider-onboarding`.
- Aplicacao:
  - `ConsertaPraMim.Application/Services/ProviderOnboardingService.cs`
  - contratos em `ConsertaPraMim.Application/Interfaces/IProviderOnboardingService.cs`.
- Dominio:
  - `ProviderProfile` (status e metadados de onboarding).
  - `ProviderOnboardingDocument` (documentos enviados).
  - enums: `ProviderOnboardingStatus`, `ProviderDocumentType`, `ProviderDocumentStatus`, `ProviderPlan`.
- Infra:
  - mapeamento EF em `ConsertaPraMim.Infrastructure/Data/ConsertaPraMimDbContext.cs`.
  - migration inicial do onboarding: `20260213174325_AddProviderOnboardingWizard`.

## Endpoints de onboarding

- `GET /api/provider-onboarding`
  - retorna estado atual do wizard, documentos e ofertas de plano.
- `PUT /api/provider-onboarding/basic-data`
  - persiste nome e telefone.
- `PUT /api/provider-onboarding/plan`
  - persiste plano selecionado (`Bronze|Silver|Gold`).
- `POST /api/provider-onboarding/documents`
  - upload de documento com validacoes e sanitizacao de nome.
- `DELETE /api/provider-onboarding/documents/{documentId}`
  - remove documento do prestador.
- `POST /api/provider-onboarding/complete`
  - valida pre-condicoes e marca onboarding concluido (`PendingApproval`).

## Regras de negocio relevantes

1. Planos validos no onboarding:
   - `Bronze`, `Silver`, `Gold`.
   - `Trial` nao pode ser escolhido manualmente no wizard.
2. Conclusao exige:
   - dados basicos preenchidos;
   - plano valido selecionado;
   - documentos obrigatorios:
     - `IdentityDocument`
     - `SelfieWithDocument`.
3. Limite de documentos por prestador:
   - maximo de `6` documentos no onboarding.

## Regras de upload e seguranca

- extensoes aceitas: `.jpg`, `.jpeg`, `.png`, `.pdf`;
- MIME types aceitos: `image/jpeg`, `image/png`, `application/pdf`;
- limite por arquivo: `10 MB`;
- sanitizacao de nome de arquivo:
  - remove path traversal (`../`);
  - remove caracteres nao permitidos;
  - limita tamanho do nome base.
- hash SHA-256 calculado no upload para rastreabilidade.

## Cobertura automatizada

- unitarios existentes:
  - `ProviderOnboardingServiceTests`
  - `ProviderOnboardingControllerTests`.
- integracao adicionada na ST-001:
  - `ProviderOnboardingApiE2ETests` cobrindo:
    - obter estado;
    - salvar plano (valido/invalido);
    - upload de documentos obrigatorios;
    - conclusao do onboarding;
    - validacao negativa de MIME.

## Observabilidade

- requests de onboarding seguem middleware global de `X-Correlation-ID` no API;
- logs estruturados da API podem ser correlacionados por `CorrelationId` no diagnostico.
