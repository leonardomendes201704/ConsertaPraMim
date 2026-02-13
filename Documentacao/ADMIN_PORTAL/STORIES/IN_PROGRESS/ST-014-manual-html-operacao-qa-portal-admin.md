# ST-014 - Manual HTML completo de operacao e QA do Portal Admin

Status: In Progress  
Epic: EPIC-005

## Objetivo

Como QA/operador administrativo, quero um manual visual e completo dentro do Portal Admin para entender cada fluxo, saber como operar, como testar e qual resultado deve ser obtido em cada acao.

## Criterios de aceite

- Existe item de menu no Portal Admin para abrir o manual.
- Manual em HTML com layout iconificado/colorido e leitura orientada a operacao.
- Manual cobre todos os modulos do Portal Admin:
  - Dashboard;
  - Usuarios;
  - Pedidos;
  - Propostas;
  - Conversas;
  - Categorias de servico;
  - Planos e ofertas.
- Cada modulo contem:
  - objetivo funcional;
  - como acessar;
  - como usar;
  - casos de uso;
  - casos de teste QA;
  - resultado esperado.
- Manual contem historias em nivel de usuario para os fluxos principais.
- Manual contem checklist de smoke test e regressao.
- Manual contem secao de troubleshooting com erros comuns e acao recomendada.
- Regra formal registrada: qualquer mudanca de funcionalidade Admin exige atualizacao do manual no mesmo ciclo de entrega.

## Tasks

- [x] Criar epic e story para o manual operacional/QA do Admin.
- [x] Implementar pagina HTML do manual dentro do `ConsertaPraMim.Web.Admin`.
- [x] Adicionar entrada de menu no Portal Admin para acesso ao manual.
- [x] Documentar uso detalhado de todos os modulos atuais do Admin.
- [x] Incluir casos de uso/historias no manual.
- [x] Incluir roteiro de testes QA (smoke e regressao) por modulo.
- [x] Incluir checklist operacional e criterios de aceite por fluxo.
- [x] Incluir troubleshooting operacional de erros comuns.
- [x] Registrar politica de atualizacao obrigatoria do manual para qualquer mudanca no Admin.
- [ ] Revisar periodicamente o manual a cada nova story concluida no Admin (rotina continua).
