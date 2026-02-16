# ST-013 - Gestao administrativa de precos, promocoes, cupons e limites por plano

Status: Done  
Epic: EPIC-004

## Objetivo

Como administrador, quero configurar preco mensal, promocoes e cupons dos planos, alem de controlar limites operacionais (raio maximo e categorias permitidas) para cada plano de prestador.

## Criterios de aceite

- Admin consegue editar o valor mensal de cada plano (`Bronze`, `Silver`, `Gold`).
- Admin consegue criar/editar/desativar promocoes com:
  - plano alvo;
  - tipo de desconto (percentual ou valor fixo);
  - data de inicio e data de fim.
- Admin consegue criar/editar/desativar cupons com:
  - codigo unico;
  - tipo de desconto (percentual ou valor fixo);
  - validade (inicio/fim);
  - limite de uso (global e/ou por prestador).
- Sistema calcula preco final da assinatura com regra clara de aplicacao:
  - preco base do plano;
  - promocao vigente;
  - cupom informado;
  - piso minimo `R$ 0,00`.
- Admin consegue configurar por plano:
  - raio maximo de atuacao permitido;
  - quantidade maxima de categorias que o prestador pode selecionar;
  - lista de categorias permitidas no plano.
- Fluxo de onboarding/perfil do prestador respeita os limites do plano configurados pelo admin.
- Alteracoes administrativas ficam auditadas (quem, quando, antes/depois).

## Tasks

- [x] Modelar entidades/configuracoes para:
  - parametros comerciais de plano;
  - promocoes com vigencia;
  - cupons de desconto;
  - regras operacionais por plano (raio maximo, categorias permitidas, limite de categorias).
- [x] Criar migration para novas estruturas de dados de planos/promocoes/cupons.
- [x] Criar seed inicial com configuracao default de precos e limites por plano.
- [x] Implementar API Admin para CRUD de configuracao comercial por plano.
- [x] Implementar API Admin para CRUD de promocoes com validacao de vigencia.
- [x] Implementar API Admin para CRUD de cupons com validacao de codigo unico e limites de uso.
- [x] Implementar servico de calculo de preco final da assinatura com ordem de aplicacao definida.
- [x] Expor endpoint de simulacao de preco (plano + data + cupom) para apoio operacional/admin.
- [x] Implementar API Admin para editar limites operacionais por plano:
  - raio maximo;
  - categorias permitidas;
  - quantidade maxima de categorias.
- [x] Aplicar validacoes dos limites de plano no onboarding/perfil do prestador.
- [x] Definir estrategia para prestadores ja existentes fora da nova regra:
  - manter funcionando sem quebra imediata;
  - bloquear novas alteracoes que violem limite;
  - sinalizar pendencia de adequacao.
- [x] Atualizar telas do portal admin para gestao de:
  - precos;
  - promocoes;
  - cupons;
  - limites operacionais.
- [x] Adicionar trilha de auditoria para alteracoes comerciais e operacionais de plano.
- [x] Criar testes unitarios para calculo de preco (promocao + cupom + limites).
- [x] Criar testes de integracao para APIs administrativas de planos/promocoes/cupons.
- [x] Criar testes de regressao para validacao dos limites operacionais no fluxo do prestador.
- [x] Atualizar INDEX/changelog do board administrativo.

## Progresso desta iteracao

- Suite de integracao criada para as APIs administrativas de governanca de planos em `AdminPlanGovernanceControllerSqliteIntegrationTests`.
- Fluxos cobertos em integracao: configuracao de plano, CRUD/status de promocoes e CRUD/status de cupons (incluindo bloqueio de codigo duplicado).
- Persistencia e trilha de auditoria validadas em banco SQLite em memoria para os cenarios administrativos principais.
