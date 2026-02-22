namespace ConsertaPraMim.Infrastructure.Data;

internal static class LegalTermsSeedContent
{
    public static string ClientV1Html => """
<section>
  <h1>Termo de Cadastro e Uso - Cliente (v1)</h1>
  <p><strong>Ultima atualizacao:</strong> 22/02/2026</p>
  <p>Este termo regula o uso da plataforma ConsertaPraMim por clientes que solicitam servicos.</p>

  <h2>1. Objeto</h2>
  <p>A ConsertaPraMim disponibiliza ambiente digital para aproximar clientes e prestadores, incluindo cadastro, solicitacoes, propostas, agendamentos, comunicacao e historico operacional.</p>

  <h2>2. Elegibilidade e dados de cadastro</h2>
  <p>O cliente declara que as informacoes fornecidas sao verdadeiras, completas e atualizadas, sendo responsavel por manter seus dados corretos e por proteger suas credenciais de acesso.</p>

  <h2>3. Regras de uso da plataforma</h2>
  <p>E vedado utilizar a plataforma para fraude, assedio, discriminacao, simulacao de identidade, tentativa de invasao, uso automatizado abusivo ou qualquer ato contrario a lei e a boa-fe.</p>

  <h2>4. Relacao com prestadores</h2>
  <p>A contratacao e realizada entre cliente e prestador. O cliente e responsavel por validar preco, escopo, prazo, condicoes de atendimento e eventuais documentos exigidos para a execucao do servico.</p>

  <h2>5. Isencao de responsabilidade da plataforma</h2>
  <p><strong>A ConsertaPraMim atua exclusivamente como plataforma de intermedicao digital e nao integra a relacao contratual material entre cliente e prestador.</strong> Assim, a plataforma nao responde por qualidade tecnica, prazos, conduta, inadimplemento, danos materiais, danos morais, acidentes, prejuizos indiretos, perdas financeiras, atos ilicitos, garantias particulares, obrigacoes tributarias ou qualquer evento decorrente da execucao do servico por qualquer das partes.</p>
  <p><strong>Cliente e prestador assumem integral responsabilidade civil, administrativa e criminal por seus atos, omissoes, negociacoes, combinados e resultados da prestacao.</strong></p>

  <h2>6. Pagamentos e comprovacoes</h2>
  <p>Quando aplicavel, o cliente e responsavel por conferir valores e comprovacoes antes de finalizar operacoes financeiras. Estornos, contestacoes e acordos especificos podem depender de analise operacional e de evidencias.</p>

  <h2>7. Avaliacoes, evidencias e auditoria</h2>
  <p>A plataforma pode registrar metadados operacionais, historico de interacoes e evidencias para fins de seguranca, suporte, conformidade e melhoria de servicos, conforme legislacao aplicavel.</p>

  <h2>8. Privacidade e protecao de dados</h2>
  <p>Dados pessoais sao tratados para execucao das funcionalidades da plataforma, seguranca, prevencao a fraude e obrigacoes legais. O cliente declara ciencia sobre este tratamento e sobre o compartilhamento minimo necessario com prestadores para viabilizar o atendimento.</p>

  <h2>9. Suspensao e encerramento</h2>
  <p>A ConsertaPraMim pode limitar, suspender ou encerrar contas em caso de violacao deste termo, risco operacional relevante ou determinacao legal/regulatoria.</p>

  <h2>10. Alteracoes de versao</h2>
  <p>Novas versoes podem ser publicadas para adequacao operacional, legal ou regulatoria. O uso continuado pode exigir aceite adicional de versao mais recente.</p>

  <h2>11. Lei aplicavel e foro</h2>
  <p>Aplica-se a legislacao brasileira. Eventuais controverias serao tratadas no foro competente conforme a lei, sem prejuizo de metodos consensuais de resolucao.</p>

  <h2>12. Declaracao de aceite</h2>
  <p>Ao marcar o aceite, o cliente declara leitura integral, compreensao e concordancia com todas as clausulas, especialmente a clausula de isencao de responsabilidade da plataforma.</p>
</section>
""";

    public static string ProviderV1Html => """
<section>
  <h1>Termo de Cadastro e Uso - Prestador (v1)</h1>
  <p><strong>Ultima atualizacao:</strong> 22/02/2026</p>
  <p>Este termo regula o uso da plataforma ConsertaPraMim por prestadores de servicos.</p>

  <h2>1. Objeto</h2>
  <p>A ConsertaPraMim fornece ambiente digital para divulgacao, recebimento de demandas, envio de propostas, agendamentos e comunicacao com clientes.</p>

  <h2>2. Declaracoes do prestador</h2>
  <p>O prestador declara possuir capacidade tecnica, regularidade para exercicio da atividade e responsabilidade sobre informacoes, documentos, certificacoes, licencas e obrigacoes legais relacionadas ao servico oferecido.</p>

  <h2>3. Obrigações operacionais</h2>
  <p>O prestador deve manter perfil atualizado, cumprir prazos acordados, observar regras de conduta e atuar com diligencia, transparencia e boa-fe em todas as interacoes.</p>

  <h2>4. Responsabilidade por propostas e execucao</h2>
  <p>Preco, escopo, materiais, garantia, cronograma, visitas, custos adicionais e resultado tecnico sao de exclusiva responsabilidade do prestador perante o cliente e autoridades competentes.</p>

  <h2>5. Isencao de responsabilidade da plataforma</h2>
  <p><strong>A ConsertaPraMim nao e empregadora, tomadora do servico, mandataria, fiadora, seguradora ou parte contratante da execucao material dos servicos.</strong> A plataforma nao responde por inadimplemento, falhas tecnicas, atrasos, danos, acidentes, perdas financeiras, defeitos, conduta de cliente, conduta de prestador, obrigacoes trabalhistas, previdenciarias, tributarias, civeis ou criminais decorrentes da relacao entre as partes.</p>
  <p><strong>Prestador e cliente assumem integral responsabilidade pelos eventos decorrentes de suas condutas e negociacoes, incluindo atos de seus prepostos e terceiros vinculados.</strong></p>

  <h2>6. Planos, tarifas e creditos</h2>
  <p>Conforme funcionalidades habilitadas, o prestador reconhece regras comerciais aplicaveis (planos, creditos, abatimentos, promocoes e limites), nos termos vigentes na plataforma.</p>

  <h2>7. Atendimento, suporte e evidencias</h2>
  <p>A plataforma pode registrar eventos operacionais, mensagens e anexos para tratamento de disputas, seguranca, auditoria e conformidade.</p>

  <h2>8. Privacidade e protecao de dados</h2>
  <p>O prestador concorda com o tratamento de dados necessario para operacao do servico e compromete-se a tratar dados de clientes em conformidade com a legislacao aplicavel, inclusive sigilo e finalidade legitima.</p>

  <h2>9. Suspensao, bloqueio e encerramento</h2>
  <p>A ConsertaPraMim pode aplicar medidas de moderacao, suspensao ou encerramento da conta em caso de violacao de regras, risco operacional, fraude, conduta abusiva ou exigencia legal.</p>

  <h2>10. Alteracoes de versao</h2>
  <p>Este termo pode ser atualizado. A continuidade de uso pode depender de novo aceite expresso da versao vigente.</p>

  <h2>11. Lei aplicavel e foro</h2>
  <p>Aplica-se a legislacao brasileira e o foro competente previsto em lei, sem prejuizo de mecanismos consensuais de resolucao de conflitos.</p>

  <h2>12. Declaracao de aceite</h2>
  <p>Ao aceitar, o prestador confirma leitura integral e concordancia com todas as clausulas, inclusive a clausula de isencao de responsabilidade da plataforma.</p>
</section>
""";
}
