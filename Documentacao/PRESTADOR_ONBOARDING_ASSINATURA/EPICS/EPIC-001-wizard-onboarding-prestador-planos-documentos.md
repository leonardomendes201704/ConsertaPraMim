# EPIC-001 - Wizard de onboarding do prestador com planos e documentos

## Objetivo

Criar um fluxo de cadastro orientado (wizard) para novo prestador, com selecao de plano de assinatura e upload de documentos de identificacao, garantindo UX simples e base tecnica segura para ativacao da conta.

## Problema atual

- Cadastro de prestador nao contempla selecao de plano no onboarding.
- Nao existe etapa dedicada para coleta de documentos de identificacao.
- Falta trilha de validacao para ativar conta com menor risco operacional e juridico.
- Fluxo atual nao guia o usuario por etapas claras, aumentando abandono.

## Resultado esperado

- Novo prestador conclui cadastro em wizard de etapas curtas e objetivas.
- Prestador escolhe 1 entre 3 planos de assinatura durante onboarding.
- Prestador envia documentos obrigatorios com validacoes de formato e tamanho.
- Conta fica com status coerente (`PendenteDocumentacao`, `PendenteAprovacao` ou `Ativa`, conforme regra).

## Metricas de sucesso

- Aumento da taxa de conclusao de cadastro de prestador em pelo menos 20%.
- 95%+ dos cadastros com documentos validos no primeiro envio.
- Tempo medio de conclusao do onboarding abaixo de 5 minutos.
- 0 regressao no login/cadastro existente de cliente e admin.

## Escopo

### Inclui

- Definicao de 3 planos de assinatura no dominio e seed.
- Persistencia da escolha do plano no perfil/conta do prestador.
- Wizard multi-step no portal do prestador para cadastro inicial.
- Upload seguro de documentos (imagem/PDF) com metadados.
- Status de onboarding/documentacao e regras de bloqueio de acesso parcial.
- Validacoes de negocio, seguranca e auditoria basica.
- Testes unitarios/integracao para regras criticas.

### Nao inclui

- Integracao com gateway de pagamento real (assinatura recorrente real).
- OCR/biometria automatica de documentos.
- Aprovacao manual via novo modulo administrativo completo (pode entrar em epic futuro).

## Historias vinculadas

- ST-001 - Cadastro do prestador com wizard, escolha de plano e envio de documentos.
