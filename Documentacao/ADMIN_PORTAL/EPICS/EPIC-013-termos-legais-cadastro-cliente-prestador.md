# EPIC-013 - Termos Legais com aceite obrigatorio (Cliente/Prestador)

Status: In Progress
Owner: Admin Platform

## Objetivo

Garantir respaldo juridico e rastreabilidade do aceite de termos no ecossistema ConsertaPraMim, exigindo leitura/aceite de termos versionados no cadastro de cliente e prestador (web e mobile), com secao explicita de isencao de responsabilidade da plataforma e governanca de edicao/publicacao pelo portal admin.

## Escopo

- Backend:
  - modelo versionado de termos por publico (`cliente` e `prestador`);
  - registro de aceite por usuario com data/hora, versao e origem;
  - APIs publicas para consulta do termo ativo;
  - APIs admin para criar/editar/publicar versoes.
- Cadastro E2E:
  - cliente e prestador so conseguem concluir cadastro ao aceitar o termo ativo;
  - validacao no backend (nao apenas UI).
- Frontends:
  - portal cliente e portal prestador exibem termo HTML e checkbox obrigatorio;
  - app cliente e app prestador exibem termo e aceite obrigatorio;
  - area no portal admin para manter os termos.
- Conteudo juridico:
  - termo de cliente e termo de prestador com clausulas de uso, privacidade e isencao de responsabilidade da plataforma nas relacoes entre partes.

## Criterios de sucesso

- Nao e possivel cadastrar cliente/prestador sem aceite valido do termo ativo.
- O aceite fica persistido e auditavel por usuario e versao.
- O admin consegue versionar/publicar novos termos sem deploy de codigo.
- Portais e apps mobile apresentam o mesmo termo ativo por publico.
- Clausula de isencao de responsabilidade da plataforma aparece de forma destacada nos dois termos.

## Stories vinculadas

- [ST-035 - Termos de aceite versionados no cadastro (web/mobile) e gestao no admin](../STORIES/IN_PROGRESS/ST-035-termos-aceite-versionado-cadastro-e-admin.md)
