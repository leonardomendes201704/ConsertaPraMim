# Runbook QA - Galeria Publica Base64 (ST-019)

## Objetivo

Validar o endpoint publico que retorna todas as fotos dos albuns de um prestador em Base64.

## Endpoint alvo

- `GET /api/provider-gallery/public/providers/{providerId}/albums/photos/base64`
- Autenticacao: nao exige token (`AllowAnonymous`).

## Pre-condicoes

1. Ter um prestador ativo com pelo menos 1 foto na galeria (`/uploads/provider-gallery/...`).
2. Opcionalmente, manter 1 item com arquivo ausente para validar `unavailablePhotosCount`.
3. API em execucao no ambiente de QA/staging.

## Checklist de validacao funcional

1. Chamar o endpoint com `providerId` valido.
2. Validar resposta `200 OK`.
3. Validar contrato:
   - `providerId`;
   - `albums[]`;
   - `albums[].photos[]`;
   - `totalPhotos`;
   - `unavailablePhotosCount`;
   - `generatedAtUtc`.
4. Confirmar que apenas fotos (`image/*`) foram retornadas.
5. Decodificar um `base64Content` e validar que o binario abre como imagem valida.
6. Validar comportamento com arquivo ausente:
   - item nao deve quebrar a resposta;
   - `unavailablePhotosCount` deve ser incrementado.
7. Chamar com `providerId` vazio/invalido e validar `400 BadRequest`.

## Checklist de seguranca minima

1. Garantir que a resolucao de arquivo aceita apenas caminho de `uploads/provider-gallery`.
2. Validar que nao ha exposicao de caminho absoluto do servidor no payload.
3. Confirmar que a API nao retorna videos/documentos nesse endpoint.

## Troubleshooting

- `400 invalid_provider`: `providerId` nao informado ou invalido.
- `totalPhotos = 0`:
  - verificar se o prestador possui itens de imagem na galeria;
  - verificar se os arquivos existem fisicamente em `wwwroot/uploads/provider-gallery`.
- `unavailablePhotosCount` alto:
  - revisar arquivos removidos manualmente;
  - revisar integridade entre banco e storage.
