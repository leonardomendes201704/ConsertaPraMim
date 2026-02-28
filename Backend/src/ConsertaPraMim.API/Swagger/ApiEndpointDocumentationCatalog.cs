using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace ConsertaPraMim.API.Swagger;

public static class ApiEndpointDocumentationCatalog
{
    private sealed record CatalogEntry(
        string DomainTitle,
        string ResourceLabel,
        string BusinessContext,
        string TechnicalContext,
        string Audience,
        IReadOnlyList<string> Rules);

    public sealed record EndpointDocumentationContext(
        string DomainTitle,
        string ResourceLabel,
        string BusinessContext,
        string TechnicalContext,
        string Audience,
        IReadOnlyList<string> Rules);

    public sealed record TagDocumentationContext(string TagName, string Description);
    public sealed record OperationNarrativeContext(
        string BusinessObjective,
        string Scenario,
        string ExpectedOutcome);

    public static EndpointDocumentationContext Resolve(ApiDescription apiDescription)
    {
        var controller = apiDescription.ActionDescriptor.RouteValues.TryGetValue("controller", out var controllerName)
            ? controllerName ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(controller))
        {
            var fallback = BuildFallbackContext("API", "recursos da API");
            return new EndpointDocumentationContext(
                DomainTitle: fallback.DomainTitle,
                ResourceLabel: fallback.ResourceLabel,
                BusinessContext: fallback.BusinessContext,
                TechnicalContext: fallback.TechnicalContext,
                Audience: fallback.Audience,
                Rules: fallback.Rules);
        }

        var entry = ResolveByController(controller);
        return new EndpointDocumentationContext(
            DomainTitle: entry.DomainTitle,
            ResourceLabel: entry.ResourceLabel,
            BusinessContext: entry.BusinessContext,
            TechnicalContext: entry.TechnicalContext,
            Audience: entry.Audience,
            Rules: entry.Rules);
    }

    public static TagDocumentationContext ResolveTag(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return new TagDocumentationContext("API", "Endpoints gerais da API ConsertaPraMim.");
        }

        var entry = ResolveByController(tagName);
        var description = $"{entry.DomainTitle}. {entry.BusinessContext} {entry.TechnicalContext}";
        return new TagDocumentationContext(tagName, description);
    }

    public static OperationNarrativeContext ResolveOperationNarrative(
        ApiDescription apiDescription,
        EndpointDocumentationContext endpointContext,
        string httpMethod,
        string normalizedPath,
        bool hasIdentifier)
    {
        var actionName = apiDescription.ActionDescriptor.RouteValues.TryGetValue("action", out var action)
            ? action ?? string.Empty
            : string.Empty;

        var actionLower = actionName.ToLowerInvariant();
        var path = normalizedPath.ToLowerInvariant();

        if (path.Contains("/api/auth/login", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Autenticar usuario da plataforma e iniciar sessao segura com token JWT.",
                Scenario: "Entrada principal de acesso para Cliente, Prestador e Admin antes de consumir modulos protegidos.",
                ExpectedOutcome: "Token valido e dados de sessao disponiveis para autorizacao dos fluxos seguintes.");
        }

        if (path.Contains("/api/auth/register", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Cadastrar novo usuario no marketplace com dados iniciais validos para operacao.",
                Scenario: "Usado no onboarding de novos participantes da plataforma (cliente/prestador).",
                ExpectedOutcome: "Conta criada com identidade persistida e pronta para autenticacao.");
        }

        if (path.Contains("/api/admin/users/admin", StringComparison.Ordinal) && httpMethod == "POST")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Provisionar conta administrativa para ampliar capacidade operacional do portal admin com trilha auditavel.",
                Scenario: "Admin responsavel cria um novo operador com role `Admin` diretamente na gestao de usuarios, sem expor cadastro publico.",
                ExpectedOutcome: "Usuario admin criado com senha segura, status ativo inicial e registro de auditoria vinculado ao ator da acao.");
        }

        if (path.Contains("/api/admin/dashboard", StringComparison.Ordinal) &&
            path.Contains("/widgets/", StringComparison.Ordinal) &&
            httpMethod == "GET")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Carregar widget isolado da home admin sem bloquear listas, tabelas e cards vizinhos.",
                Scenario: "Portal admin consulta componentes analiticos como receita, reputacao, falhas de pagamento e eventos recentes em chamadas dedicadas, preservando o mesmo recorte global de filtros.",
                ExpectedOutcome: "Payload enxuto do widget com dados suficientes para tabela, lista ou grade de eventos, pronto para skeleton/spinner e tratamento de erro localizado.");
        }

        if (path.Contains("/api/admin/dashboard", StringComparison.Ordinal) &&
            path.Contains("/kpis/", StringComparison.Ordinal) &&
            httpMethod == "GET")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Carregar KPI isolado da home admin para renderizacao incremental sem bloquear os demais cards executivos.",
                Scenario: "Portal admin consulta um card especifico (`usuarios`, `agenda`, `creditos`, `NPS`) preservando filtros globais e exibindo skeleton/spinner por componente.",
                ExpectedOutcome: "Payload enxuto do KPI com valor principal, caption e linhas auxiliares, pronto para refresh seletivo e tratamento de erro localizado.");
        }

        if (path.Contains("/api/admin/dashboard", StringComparison.Ordinal) &&
            !path.Contains("/coverage-map", StringComparison.Ordinal) &&
            httpMethod == "GET")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Consolidar desempenho operacional e sinais de retencao do marketplace em uma unica visao executiva.",
                Scenario: "Lideranca/admin acompanha volume de pedidos, reputacao, no-show, recompras e NPS operacional para orientar a rotina semanal de growth.",
                ExpectedOutcome: "Dashboard retorna KPIs de qualidade pos-servico (`operationalNpsScore`, `operationalQualityScore`) e recompra (`repurchaseRatePercent`) junto dos demais indicadores de operacao.");
        }

        if (path.Contains("/api/admin/no-show-dashboard/kpis/", StringComparison.Ordinal) &&
            httpMethod == "GET")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Carregar KPI isolado do painel de no-show para acompanhamento operacional incremental na home admin.",
                Scenario: "Cada card critico de no-show (`taxa`, `fila`, `reincidencia`, `usuarios criticos`) e consultado de forma independente no mesmo recorte aplicado pelo operador.",
                ExpectedOutcome: "Card retorna valor sintetico e detalhe auxiliar suficiente para leitura rapida, sem depender do payload completo de tabelas e queues.");
        }

        if (path.Contains("/api/service-requests/problem-analysis", StringComparison.Ordinal) && httpMethod == "POST")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Gerar entendimento assistido por IA da descricao do cliente antes da publicacao do pedido.",
                Scenario: "Passo intermediario do wizard do portal cliente para validar se a categoria escolhida e os detalhes textuais fazem sentido operacional.",
                ExpectedOutcome: "Resumo curto do problema com highlights tecnicos para o cliente revisar antes de avancar para endereco e publicacao; esse conteudo pode ser persistido no pedido para contexto do prestador.");
        }

        if (path.Contains("/api/service-requests/zip-resolution", StringComparison.Ordinal) && httpMethod == "GET")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Resolver CEP para referencia geografica operacional do pedido antes da publicacao.",
                Scenario: "Passo `Onde?` do wizard de abertura no portal cliente para preencher rua, bairro e cidade, alem de coordenadas para matching geografico.",
                ExpectedOutcome: "Resposta com `zipCode`, `street`, `neighborhood`, `city`, `latitude` e `longitude`, permitindo exibir mapa com raio de busca sem expor o endereco completo no fluxo inicial.");
        }

        if (path.Contains("/api/service-requests/", StringComparison.Ordinal) &&
            path.Contains("/cancel", StringComparison.Ordinal) &&
            httpMethod == "POST")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Encerrar o pedido inteiro quando o cliente desistiu do atendimento antes da janela operacional critica.",
                Scenario: "Cliente cancela o pedido na tela de detalhes; a API valida todos os agendamentos ativos, cancela em cascata os elegiveis, fecha o pedido como `Canceled` e avisa os prestadores com interacao.",
                ExpectedOutcome: "Pedido encerrado de forma definitiva, sem retorno para `Matching`, com cancelamento consistente dos agendamentos vinculados e fan-out de notificacoes para os prestadores impactados.");
        }

        if (path.Contains("/api/service-requests", StringComparison.Ordinal) && httpMethod == "POST")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Abrir um novo pedido de servico para iniciar o ciclo comercial cliente -> prestador.",
                Scenario: "Cliente informa categoria, descricao e localizacao para encontrar prestadores elegiveis.",
                ExpectedOutcome: "Pedido registrado em estado inicial e apto a receber propostas, incluindo resumo/highlights da analise inicial quando enviados pelo wizard.");
        }

        if (path.Contains("/api/service-requests", StringComparison.Ordinal) && httpMethod == "GET" && !hasIdentifier)
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Consultar carteira de pedidos para acompanhamento operacional e filtros de atendimento.",
                Scenario: "Utilizado por cliente/prestador/admin para listar pedidos por status, periodo ou contexto.",
                ExpectedOutcome: "Lista consistente de pedidos com metadados para decisao e proxima acao.");
        }

        if (path.Contains("/api/service-requests", StringComparison.Ordinal) && httpMethod == "GET" && hasIdentifier)
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Obter detalhes completos de um pedido para decisao de proposta, execucao ou auditoria.",
                Scenario: "Tela de detalhe com contexto unico do pedido, participante e eventos associados.",
                ExpectedOutcome: "Payload detalhado do pedido no estado atual de negocio.");
        }

        if (path.Contains("/api/proposals", StringComparison.Ordinal) && httpMethod == "POST")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Registrar proposta comercial do prestador para um pedido elegivel.",
                Scenario: "Prestador envia preco/condicoes para disputar atendimento do cliente.",
                ExpectedOutcome: "Proposta criada com rastreabilidade e status coerente com o pedido.");
        }

        if ((path.Contains("/accept", StringComparison.Ordinal) || actionLower.Contains("accept", StringComparison.Ordinal)) &&
            (path.Contains("/proposal", StringComparison.Ordinal) || path.Contains("/proposals", StringComparison.Ordinal)))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Formalizar aceite da proposta selecionada pelo cliente.",
                Scenario: "Transicao critica do ciclo comercial para etapa de agenda/execucao.",
                ExpectedOutcome: "Proposta marcada como aceita e pedido atualizado para proxima fase.");
        }

        if (path.Contains("/api/reviews/client", StringComparison.Ordinal) && httpMethod == "POST")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Registrar feedback pos-servico do cliente com questionario estruturado de qualidade e NPS operacional.",
                Scenario: "Apos conclusao paga do atendimento, cliente avalia o prestador por nota geral, dimensoes de qualidade e intencao de recompra.",
                ExpectedOutcome: "Review persistida com score composto (0-100), distribuicao por dimensao e trilha de reputacao para ranking/retencao.");
        }

        if (path.Contains("/api/reviews/provider", StringComparison.Ordinal) && httpMethod == "POST")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Registrar feedback pos-servico do prestador sobre o cliente para governanca de qualidade bilateral.",
                Scenario: "Prestador vencedor da proposta aceita conclui avaliacao da contraparte no encerramento do ciclo pago.",
                ExpectedOutcome: "Review validada sem duplicidade, com respostas estruturadas opcionais e score composto consolidado.");
        }

        if (path.Contains("/api/reviews/client/pending", StringComparison.Ordinal) && httpMethod == "GET")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Identificar pendencias de avaliacao pos-servico para reforcar coleta de feedback do cliente.",
                Scenario: "App/portal cliente consulta atendimentos concluidos e pagos ainda sem review dentro da janela de avaliacao.",
                ExpectedOutcome: "Lista priorizada de pedidos pendentes com prazo limite para avaliacao e dados da contraparte.");
        }

        if (path.Contains("/api/reviews/provider/pending", StringComparison.Ordinal) && httpMethod == "GET")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Identificar pendencias de avaliacao pos-servico para reforcar coleta de feedback do prestador.",
                Scenario: "App/portal prestador consulta atendimentos concluidos e pagos ainda sem review no ciclo de pos-servico.",
                ExpectedOutcome: "Backlog de avaliacoes pendentes por prestador com janela restante para envio do feedback.");
        }

        if (path.Contains("/api/reviews/admin/repurchase/run", StringComparison.Ordinal) && httpMethod == "POST")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Ativar recompra de clientes com alto potencial de retencao apos servico concluido.",
                Scenario: "Operacao admin dispara janela de recompra para pedidos concluidos/pagos sem nova demanda, priorizando experiencias positivas.",
                ExpectedOutcome: "Execucao retorna candidatos elegiveis, disparos realizados e motivos de supressao (ja recomprou, sem review positiva, ja acionado).");
        }

        if (path.Contains("/api/reviews/summary/provider", StringComparison.Ordinal) && httpMethod == "GET")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Consolidar reputacao do prestador para apoiar decisao de aceite e ranking operacional.",
                Scenario: "Clientes e operacao consultam media/distribuicao de notas para reduzir risco de conversao.",
                ExpectedOutcome: "Resumo com media, volume e distribuicao por estrelas pronto para exibicao de reputacao.");
        }

        if (path.Contains("/api/reviews/summary/client", StringComparison.Ordinal) && httpMethod == "GET")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Consolidar historico de reputacao do cliente para leitura de risco comportamental.",
                Scenario: "Prestador e operacao consultam distribuicao de notas da contraparte no ciclo comercial.",
                ExpectedOutcome: "Resumo estatistico consistente para suporte a governanca de atendimento.");
        }

        if (path.Contains("/proposals/comparison/interactions", StringComparison.Ordinal) && httpMethod == "POST")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Registrar evento de uso do comparador de propostas para analise de conversao.",
                Scenario: "App cliente envia eventos de interacao (view/sort/open/accept) para telemetria operacional.",
                ExpectedOutcome: "Evento persistido com grupo de experimento e metadados para analise A/B.");
        }

        if (path.Contains("/proposals/comparison", StringComparison.Ordinal) && httpMethod == "GET")
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Entregar comparativo estruturado das propostas do pedido para decisao mais rapida do cliente.",
                Scenario: "Tela de detalhe do pedido consulta ranking por score, preco, prazo, avaliacao e garantia.",
                ExpectedOutcome: "Payload consolidado do comparador com ordenacao aplicada e resumo de diferencas.");
        }

        if (path.Contains("/api/admin/proposal-comparison/ab-summary", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Consolidar impacto do comparador em experimento A/B para validar ganho de conversao.",
                Scenario: "Admin consulta janela temporal para comparar volume de interacoes e aceite apos comparacao por bucket.",
                ExpectedOutcome: "Resumo por grupo (`control`/`variant`) com taxa de conversao e volume de eventos.");
        }

        if (path.Contains("/slots", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Consultar disponibilidade de agenda para viabilizar agendamento do servico.",
                Scenario: "Cliente verifica janelas de atendimento apos escolher proposta.",
                ExpectedOutcome: "Retorno de slots validos com base na agenda atual do prestador.");
        }

        if (path.Contains("/schedule", StringComparison.Ordinal) || actionLower.Contains("schedule", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Agendar atendimento com base na proposta aceita e slot disponivel.",
                Scenario: "Cliente confirma data/horario para execucao do servico contratado.",
                ExpectedOutcome: "Atendimento agendado com status sincronizado em pedido, proposta e agenda.");
        }

        if (path.Contains("/api/chats", StringComparison.Ordinal) || path.Contains("/chat", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Manter comunicacao operacional entre cliente e prestador durante o ciclo do pedido.",
                Scenario: "Troca de mensagens e anexos para alinhamento, evidencias e suporte da execucao.",
                ExpectedOutcome: "Conversa persistida com historico temporal e trilha auditavel.");
        }

        if (path.Contains("/api/service-appointments", StringComparison.Ordinal) && path.Contains("/dispute", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Registrar ou operar disputa de atendimento para mediacao administrativa.",
                Scenario: "Cliente/prestador aciona contestacao apos conflito na execucao do servico.",
                ExpectedOutcome: "Disputa vinculada ao atendimento com estado e evidencia para decisao admin.");
        }

        if (path.Contains("/api/admin/disputes", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Operar fila de disputas com triagem, workflow e decisao auditavel.",
                Scenario: "Admin analisa casos sensiveis, aplica decisao e aciona impacto financeiro quando previsto.",
                ExpectedOutcome: "Caso atualizado com trilha de decisao e notificacao das partes envolvidas.");
        }

        if (path.Contains("/api/admin/support-tickets", StringComparison.Ordinal) || path.Contains("/support-ticket", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Gerenciar atendimento de suporte entre operacao admin e prestadores/clientes.",
                Scenario: "Fila de suporte para resposta, atribuicao, mudanca de status e historico de mensagens.",
                ExpectedOutcome: "Chamado atualizado com SLA operacional e rastreabilidade de interacoes.");
        }

        if (path.Contains("/api/admin/plan-governance/revenue-components", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Consolidar receita do modelo hibrido separando componente fixo (assinatura) e variavel (creditos).",
                Scenario: "Operacao comercial/financeira analisa o recorte temporal para ajustar planos, promocoes e campanhas de resultado.",
                ExpectedOutcome: "Dashboard com MRR fixo por plano, receita variavel realizada no ledger e serie diaria para tomada de decisao.");
        }

        if (path.Contains("/api/admin/plan-governance/hybrid-rollout", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Definir rollout progressivo do modelo hibrido por cohort de prestadores com governanca de risco.",
                Scenario: "Admin comercial/operacao avalia elegibilidade por trust/compliance/plano e decide fases de liberacao.",
                ExpectedOutcome: "Plano de rollout com cohorts priorizados, metas por fase e guardrails para evitar regressao operacional.");
        }

        if (path.Contains("/api/admin/plan-governance", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Governar regras de planos, promocoes e cupons do marketplace sem necessidade de deploy.",
                Scenario: "Admin ajusta parametros comerciais/operacionais e simula impacto de preco para prestadores.",
                ExpectedOutcome: "Politica comercial atualizada com rastreabilidade administrativa e simulacao coerente com as regras ativas.");
        }

        if (path.Contains("/api/admin/mailbox", StringComparison.Ordinal) || path.Contains("/mailbox", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Executar fluxo de webmail administrativo para comunicacao oficial com usuarios.",
                Scenario: "Admin configura SMTP/POP3, sincroniza caixa e envia comunicacoes de negocio.",
                ExpectedOutcome: "Mensagem processada com status de envio/sync e historico operacional.");
        }

        if (path.Contains("/push-devices", StringComparison.Ordinal) && httpMethod == "POST" && (path.Contains("/register", StringComparison.Ordinal) || actionLower.Contains("register", StringComparison.Ordinal)))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Registrar dispositivo/token push para entrega de notificacoes no app correspondente.",
                Scenario: "Executado no login/boot/rotacao de token para manter canal push ativo por dispositivo.",
                ExpectedOutcome: "Token associado ao usuario/instalacao com status ativo e telemetria atualizada.");
        }

        if (path.Contains("/push-devices", StringComparison.Ordinal) && httpMethod == "POST" && (path.Contains("/unregister", StringComparison.Ordinal) || actionLower.Contains("unregister", StringComparison.Ordinal)))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Desativar registro push de um dispositivo para evitar envio indevido de notificacoes.",
                Scenario: "Executado no logout, troca de conta ou revogacao do token/dispositivo.",
                ExpectedOutcome: "Dispositivo marcado como inativo/revogado para novos envios.");
        }

        if (path.Contains("/api/mobile/client/pj-recurring-contracts", StringComparison.Ordinal))
        {
            if (httpMethod == "GET")
            {
                return new OperationNarrativeContext(
                    BusinessObjective: "Consultar carteira de contratos PJ recorrentes do cliente autenticado.",
                    Scenario: "App cliente usa a listagem para acompanhar status do pacote, SLA vigente e proxima renovacao.",
                    ExpectedOutcome: "Lista consistente de contratos recorrentes com dados de ciclo, janela operacional e renovacao.");
            }

            if (path.Contains("/renew", StringComparison.Ordinal))
            {
                return new OperationNarrativeContext(
                    BusinessObjective: "Registrar renovacao de ciclo de um pacote PJ recorrente.",
                    Scenario: "Cliente PJ confirma continuidade do contrato e atualiza o proximo marco de renovacao.",
                    ExpectedOutcome: "Contrato atualizado com `LastRenewedAtUtc`, `LastPaymentAtUtc` e novo estado de renovacao.");
            }

            return new OperationNarrativeContext(
                BusinessObjective: "Contratar novo pacote PJ recorrente com SLA e janela operacional.",
                Scenario: "Cliente PJ seleciona categoria, cadencia e elegibilidade de prestadores para iniciar contrato recorrente.",
                ExpectedOutcome: "Contrato PJ criado em estado ativo, com dados de ciclo/proxima renovacao e contagem de prestadores elegiveis para execucao.");
        }

        if (path.Contains("/api/admin/monitoring", StringComparison.Ordinal) || path.Contains("/monitoring", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Expor telemetria operacional da API para acompanhamento de saude e desempenho.",
                Scenario: "Admin consulta KPIs, latencia, erros e series temporais para acao preventiva/corretiva.",
                ExpectedOutcome: "Dados consolidados de observabilidade prontos para dashboard e troubleshooting.");
        }

        if (path.Contains("/api/admin/pj-recurring-contracts/kpis/revenue", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Monitorar KPIs de receita recorrente PJ por janela temporal.",
                Scenario: "Admin financeiro/comercial acompanha renovações previstas, receita recorrente esperada e risco de inadimplencia.",
                ExpectedOutcome: "Serie diaria de renovacoes/receita prevista com visao consolidada de contratos ativos e delinquentes.");
        }

        if (path.Contains("/api/admin/pj-recurring-contracts/portfolio", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Consolidar carteira de contratos PJ recorrentes para governanca comercial e operacional.",
                Scenario: "Admin filtra periodo/status para monitorar volume de contratos, receita recorrente e risco de inadimplencia.",
                ExpectedOutcome: "Painel com KPI de carteira, breakdown por status/categoria e lista de contratos com renovacao/SLA/elegibilidade.");
        }

        if (path.Contains("/api/admin/growth/funnel", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Medir funil operacional de crescimento (pedido -> proposta -> aceite) com SLA por etapa.",
                Scenario: "Operacao e produto acompanham gargalos de liquidez e conversao por periodo/categoria/cidade.",
                ExpectedOutcome: "Indicadores de funil, taxas de SLA e alertas acionaveis para priorizacao de melhorias.");
        }

        if (path.Contains("/api/admin/growth/executive-cockpit", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Consolidar cockpit executivo de growth com North Star, metas trimestrais e tendencia semanal.",
                Scenario: "Lideranca executiva consulta uma visao unica para acompanhar `RQ72`, guardrails de conversao e sinais de risco para a rotina semanal.",
                ExpectedOutcome: "Payload retorna North Star atual, numerador/denominador, metas por trimestre, KPIs de cobertura/aceite/SLA e serie semanal para tomada de decisao.");
        }

        if (path.Contains("/api/admin/growth/liquidity-score", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Calcular score de liquidez por regiao/categoria para orientar captacao de oferta e reduzir pedidos sem proposta.",
                Scenario: "Operacao comercial e growth consultam ranking de deficit com serie historica para priorizar acoes por geografia e categoria.",
                ExpectedOutcome: "Score classificado em faixas (critical/warning/healthy), com alertas de deficit e base para playbook operacional.");
        }

        if (path.Contains("/api/admin/growth/ai/analyze", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Gerar diagnostico executivo assistido por IA combinando funil de growth e score de liquidez.",
                Scenario: "Lideranca/admin dispara rodada de analise para transformar KPI operacional em recomendacoes priorizadas de curto prazo.",
                ExpectedOutcome: "Resposta retorna resumo executivo, insights de funil, insights de liquidez, riscos e plano de acao rastreavel por recorte.");
        }

        if (path.Contains("/api/admin/growth/ai/compare", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Comparar duas rodadas de analise IA para medir evolucao operacional entre ciclos de growth.",
                Scenario: "Lideranca seleciona uma analise baseline e uma analise atual para entender ganhos, regressoes e prioridades do proximo ciclo semanal.",
                ExpectedOutcome: "Resposta retorna delta executivo com melhorias, regressoes, sinais estaveis e plano de acao priorizado.");
        }

        if (path.Contains("/api/admin/growth/ai/settings", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Governar configuracao do copiloto IA (API key, modelo, prompt e limites) sem alterar codigo.",
                Scenario: "Admin tecnico ajusta parametros de analise para manter custo, qualidade e compliance operacional.",
                ExpectedOutcome: "Configuracao persistida com mascara de segredo no retorno e pronta para uso nas proximas rodadas de analise.");
        }

        if (path.Contains("/api/admin/growth/ai/snapshot", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Expor estado atual do modulo IA para apoiar governanca de decisao no portal admin.",
                Scenario: "Tela de cockpit consulta configuracao efetiva e historico recente de analises para continuidade da rotina semanal.",
                ExpectedOutcome: "Snapshot retorna parametros ativos, status de configuracao e trilha de analises recentes com metadados.");
        }

        if (path.Contains("/api/admin/growth/provider-reactivation/segments", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Segmentar prestadores inativos por tempo sem atividade para orientar campanhas de reativacao.",
                Scenario: "Operacao de growth filtra blocos de inatividade (atencao/frio/dormente/hibernado) e identifica prioridade por categoria/regiao.",
                ExpectedOutcome: "Snapshot de inatividade com breakdown por segmento e preview de prestadores para acao operacional imediata.");
        }

        if (path.Contains("/api/admin/growth/provider-reactivation/campaigns/run", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Executar rodada de campanha de reativacao com governanca de cadencia.",
                Scenario: "Admin de growth dispara acao operacional segmentada para prestadores inativos, respeitando janela minima entre campanhas, politicas de opt-out/frequencia e escolha de canais (sistema/push/email).",
                ExpectedOutcome: "Rodada registrada com destinatarios selecionados, status de execucao, trilha de entrega por canal, contadores de supressao por politica e bloqueio automatico quando a cadencia nao permite novo disparo.");
        }

        if (path.Contains("/api/admin/growth/provider-reactivation/campaigns/performance", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Medir efetividade das campanhas de reativacao para ajustar estrategia de crescimento.",
                Scenario: "Operacao de growth acompanha historico de campanhas com volume selecionado, entregas por canal e taxa de prestadores reativados apos cada rodada.",
                ExpectedOutcome: "Painel de performance com consolidado e ranking de campanhas para priorizar segmentos/canais com melhor retorno.");
        }

        if (path.Contains("/api/admin/growth/provider-reactivation/preferences", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Aplicar governanca de frequencia e opt-out para campanhas de reativacao.",
                Scenario: "Operacao ajusta preferencia individual de prestador (opt-out e teto semanal de acionamentos) para reduzir fadiga de notificacao.",
                ExpectedOutcome: "Preferencia auditavel persistida e respeitada automaticamente nas proximas rodadas de campanha.");
        }

        if (path.Contains("/api/admin/growth/monthly-review/record", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Registrar ata executiva da revisao mensal de growth com decisoes estrategicas e plano do proximo ciclo.",
                Scenario: "Comite de growth finaliza o fechamento mensal documentando prioridades, riscos, budget e apostas para o mes seguinte.",
                ExpectedOutcome: "Ata mensal persistida em trilha auditavel com owner responsavel, contexto executivo e direcionamento para execucao.");
        }

        if (path.Contains("/api/admin/growth/monthly-review", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Expor processo padrao de revisao mensal para governanca estrategica de growth.",
                Scenario: "Lideranca consulta agenda mensal, consolida historico de atas e prepara o ciclo de decisao executiva.",
                ExpectedOutcome: "Snapshot retorna pauta oficial do fechamento mensal e registros recentes para continuidade de estrategia.");
        }

        if (path.Contains("/api/admin/growth/weekly-ritual/record", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Registrar ata da rotina semanal de growth com decisoes, owners e proximas acoes.",
                Scenario: "Lideranca encerra o ritual semanal consolidando acordos operacionais e riscos para acompanhamento.",
                ExpectedOutcome: "Ata persistida em trilha auditavel para consulta no cockpit executivo e governanca continua.");
        }

        if (path.Contains("/api/admin/growth/weekly-ritual", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Expor pauta semanal de growth e historico recente de atas para execucao disciplinada da rotina.",
                Scenario: "Time de growth consulta o quadro da semana antes da reuniao e revisita registros anteriores.",
                ExpectedOutcome: "Snapshot retorna agenda padrao e ultimas atas com owner, decisoes e proximas acoes.");
        }

        if (path.Contains("/api/admin/load-tests", StringComparison.Ordinal) || path.Contains("/load-tests", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Consultar resultados de testes de carga para validar estabilidade da plataforma.",
                Scenario: "Operacao e engenharia analisam throughput, latencia, erros e recomendacoes da execucao.",
                ExpectedOutcome: "Execucao de carga documentada com indicadores para decisao de capacidade.");
        }

        if (path.Contains("/api/admin/provider-credits", StringComparison.Ordinal) || path.Contains("/provider-credits", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Gerenciar saldo e movimentos de creditos do prestador com impacto financeiro controlado.",
                Scenario: "Admin concede, estorna e audita creditos para governanca comercial e suporte operacional.",
                ExpectedOutcome: "Ledger atualizado com consistencia e rastreabilidade por operacao.");
        }

        if (path.Contains("/api/legal-terms", StringComparison.Ordinal) || path.Contains("/legal-terms", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Publicar, consultar ou registrar aceite de termos legais versionados da plataforma.",
                Scenario: "Fluxo de compliance para garantir consentimento formal de cliente e prestador.",
                ExpectedOutcome: "Versao/aceite persistidos com trilha temporal e juridica.");
        }

        if (path.Contains("/api/profile", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Atualizar e consultar perfil do usuario para manter dados operacionais e de contato.",
                Scenario: "Usado por web/mobile para editar nome, localizacao, preferencia e aceite relacionado ao perfil.",
                ExpectedOutcome: "Perfil sincronizado no backend, refletindo informacoes atuais do usuario.");
        }

        if (path.Contains("/api/payments", StringComparison.Ordinal))
        {
            return new OperationNarrativeContext(
                BusinessObjective: "Processar etapa financeira de cobranca/pagamento conforme regra de assinatura e operacao.",
                Scenario: "Fluxos financeiros acionados por compra, renovacao ou conciliacao administrativa.",
                ExpectedOutcome: "Estado financeiro atualizado com retorno claro de sucesso/falha.");
        }

        return BuildDefaultNarrative(endpointContext, httpMethod, normalizedPath, actionName, hasIdentifier);
    }

    private static OperationNarrativeContext BuildDefaultNarrative(
        EndpointDocumentationContext endpointContext,
        string httpMethod,
        string normalizedPath,
        string actionName,
        bool hasIdentifier)
    {
        var operationText = httpMethod switch
        {
            "GET" when hasIdentifier => "consultar detalhes",
            "GET" => "consultar lista",
            "POST" => "registrar acao",
            "PUT" => "atualizar recurso completo",
            "PATCH" => "atualizar recurso parcial",
            "DELETE" => "desativar/remover recurso",
            _ => $"executar operacao {httpMethod}"
        };

        var normalizedAction = string.IsNullOrWhiteSpace(actionName) ? "nao informada" : actionName;
        return new OperationNarrativeContext(
            BusinessObjective: $"Executar {operationText} em {endpointContext.ResourceLabel} com foco no dominio {endpointContext.DomainTitle}.",
            Scenario: $"Fluxo acionado por {endpointContext.Audience} na rota `{normalizedPath}` (acao `{normalizedAction}`).",
            ExpectedOutcome: $"Resposta consistente com as regras de negocio de {endpointContext.ResourceLabel}, mantendo rastreabilidade operacional.");
    }

    private static CatalogEntry ResolveByController(string controller)
    {
        if (controller.StartsWith("Admin", StringComparison.OrdinalIgnoreCase))
        {
            return new CatalogEntry(
                DomainTitle: "Dominio Administrativo",
                ResourceLabel: BuildResourceLabel(controller, "admin"),
                BusinessContext: "Apoia operacao, governanca, auditoria e suporte do ecossistema ConsertaPraMim.",
                TechnicalContext: "Consumido pelo portal admin e por fluxos operacionais protegidos por policy de administracao.",
                Audience: "Operacao/Admin/QA/Suporte",
                Rules:
                [
                    "Priorizar rastreabilidade das acoes administrativas e motivos de alteracao.",
                    "Respeitar politicas de role/policy para evitar escalacao indevida de privilegios.",
                    "Em acao sensivel, registrar correlacao entre usuario operador e entidade afetada."
                ]);
        }

        if (controller.StartsWith("MobileClient", StringComparison.OrdinalIgnoreCase))
        {
            return new CatalogEntry(
                DomainTitle: "Dominio Mobile Cliente",
                ResourceLabel: BuildResourceLabel(controller, "cliente mobile"),
                BusinessContext: "Sustenta a jornada do cliente na abertura de pedidos, propostas, agenda e acompanhamento.",
                TechnicalContext: "Consumido principalmente pelo app cliente, com respostas orientadas a UX mobile.",
                Audience: "App Cliente/QA Mobile/Suporte",
                Rules:
                [
                    "Evitar payloads excessivos para reduzir custo de rede no mobile.",
                    "Tratar conflitos de negocio (409) com mensagens acionaveis para o usuario final.",
                    "Priorizar consistencia entre estado exibido no app e estado persistido no backend."
                ]);
        }

        if (controller.StartsWith("MobileProvider", StringComparison.OrdinalIgnoreCase))
        {
            return new CatalogEntry(
                DomainTitle: "Dominio Mobile Prestador",
                ResourceLabel: BuildResourceLabel(controller, "prestador mobile"),
                BusinessContext: "Orquestra operacao do prestador: agenda, propostas, atendimento, evidencias e suporte.",
                TechnicalContext: "Consumido pelo app do prestador com regras de onboarding, disponibilidade e compliance.",
                Audience: "App Prestador/QA Mobile/Suporte",
                Rules:
                [
                    "Endpoints de operacao devem respeitar bloqueios por onboarding e estado de assinatura/plano.",
                    "Mudancas de status precisam preservar trilha de auditoria e coerencia temporal.",
                    "Anexos/evidencias devem seguir limites de tamanho e formato definidos pela plataforma."
                ]);
        }

        if (controller.StartsWith("MobileAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return new CatalogEntry(
                DomainTitle: "Dominio Mobile Admin",
                ResourceLabel: BuildResourceLabel(controller, "admin mobile"),
                BusinessContext: "Disponibiliza monitoramento e operacoes essenciais para administradores em dispositivos moveis.",
                TechnicalContext: "Consumido pelo app admin com autenticacao forte e eventos em tempo quase real.",
                Audience: "App Admin/Operacao N1/N2",
                Rules:
                [
                    "Operacoes devem ser seguras e minimizadas para contexto mobile.",
                    "Notificacoes e eventos precisam manter correlacao com origem da acao.",
                    "Falhas de autorizacao devem ser tratadas explicitamente no cliente."
                ]);
        }

        return controller switch
        {
            "Auth" => new CatalogEntry(
                DomainTitle: "Identidade e Acesso",
                ResourceLabel: "autenticacao e sessao",
                BusinessContext: "Responsavel pelo ciclo de autenticacao, emissao de token e protecao de acesso.",
                TechnicalContext: "Base para autenticacao JWT dos portais web e apps mobile.",
                Audience: "Todos os clientes da API",
                Rules:
                [
                    "Nunca logar credenciais em texto puro.",
                    "Renovar token conforme politica de expiracao do backend.",
                    "Aplicar hardening de tentativas para reduzir abuso."
                ]),
            "Files" => new CatalogEntry(
                DomainTitle: "Arquivos e Midia",
                ResourceLabel: "upload e acesso de arquivos",
                BusinessContext: "Gerencia anexos e evidencias que suportam pedidos, atendimentos e disputas.",
                TechnicalContext: "Opera com validacao de formato/tamanho e armazenamento externo.",
                Audience: "Web/Mobile/Operacao",
                Rules:
                [
                    "Validar tipo de arquivo antes do envio.",
                    "Preservar referencia de rastreabilidade da entidade de origem.",
                    "Respeitar politicas de retencao e privacidade."
                ]),
            "Chats" or "ChatAttachments" => new CatalogEntry(
                DomainTitle: "Comunicacao em Atendimento",
                ResourceLabel: "conversas e anexos de chat",
                BusinessContext: "Permite troca de mensagens entre cliente e prestador durante o ciclo do pedido.",
                TechnicalContext: "Integrado com hub de notificacoes e armazenamento de anexos.",
                Audience: "Portais Web/Apps/Suporte",
                Rules:
                [
                    "Mensagens devem manter ordem temporal consistente.",
                    "Anexos precisam ser validados e vinculados ao chat correto.",
                    "Leitura/entrega deve considerar status de recibo quando aplicavel."
                ]),
            "Notifications" => new CatalogEntry(
                DomainTitle: "Notificacoes",
                ResourceLabel: "notificacoes de usuario",
                BusinessContext: "Distribui comunicacoes operacionais para usuarios da plataforma.",
                TechnicalContext: "Suporta leitura/listagem e integra com canais realtime/push.",
                Audience: "Web/Mobile/Admin",
                Rules:
                [
                    "Priorizar mensagens acionaveis com contexto de negocio.",
                    "Registrar eventos de entrega/falha para suporte.",
                    "Evitar duplicidade de notificacao para o mesmo evento."
                ]),
            "Payments" => new CatalogEntry(
                DomainTitle: "Pagamentos e Financeiro",
                ResourceLabel: "pagamentos e cobrancas",
                BusinessContext: "Controla etapas financeiras de assinatura, cobranca e reconciliacao.",
                TechnicalContext: "Integra com provedores externos e politicas internas de retry/conferencia.",
                Audience: "Admin/Financeiro/Suporte",
                Rules:
                [
                    "Tratar operacoes financeiras de forma idempotente quando necessario.",
                    "Registrar motivo de falha de pagamento para acao operacional.",
                    "Preservar trilha de auditoria para compliance."
                ]),
            "Profile" => new CatalogEntry(
                DomainTitle: "Perfil e Preferencias",
                ResourceLabel: "perfil do usuario",
                BusinessContext: "Centraliza dados cadastrais e preferencias de operacao do usuario autenticado.",
                TechnicalContext: "Consumido por portais/apps para manter dados em sincronia com backend.",
                Audience: "Cliente/Prestador/Admin",
                Rules:
                [
                    "Alteracoes cadastrais devem respeitar validacoes de dominio.",
                    "Dados sensiveis devem ser mascarados quando retornados ao frontend.",
                    "Persistencia deve manter consistencia com trilha de aceite/consentimento."
                ]),
            "Proposals" => new CatalogEntry(
                DomainTitle: "Propostas",
                ResourceLabel: "propostas de prestadores",
                BusinessContext: "Gerencia propostas comerciais vinculadas a pedidos de servico.",
                TechnicalContext: "Opera com regras de status, validade e integridade com agenda/pedido.",
                Audience: "Prestador/Cliente/Admin",
                Rules:
                [
                    "Evitar propostas em duplicidade para o mesmo contexto invalido.",
                    "Sincronizar transicoes de status com pedido/agendamento.",
                    "Registrar justificativas em invalidacoes administrativas."
                ]),
            "ProviderCredits" => new CatalogEntry(
                DomainTitle: "Creditos do Prestador",
                ResourceLabel: "carteira e extrato de creditos",
                BusinessContext: "Suporta concessao/consumo/estorno de creditos usados em governanca comercial.",
                TechnicalContext: "Baseado em ledger transacional com rastreabilidade de saldo e movimentos.",
                Audience: "Admin/Prestador/Financeiro",
                Rules:
                [
                    "Operacoes financeiras devem ser auditaveis ponta a ponta.",
                    "Saldo nunca pode ficar inconsistente apos mutacoes concorrentes.",
                    "Conferir tipo de lancamento antes de estorno/reversao."
                ]),
            "ProviderGallery" => new CatalogEntry(
                DomainTitle: "Galeria do Prestador",
                ResourceLabel: "portfolio de evidencias do prestador",
                BusinessContext: "Permite exibicao e manutencao de provas visuais da atuacao do prestador.",
                TechnicalContext: "Integra upload, metadados e politicas de retencao de evidencias.",
                Audience: "Prestador/Cliente/QA",
                Rules:
                [
                    "Validar formato e tamanho de midia.",
                    "Vincular evidencias ao prestador correto.",
                    "Respeitar politicas de moderacao quando aplicavel."
                ]),
            "ProviderOnboarding" => new CatalogEntry(
                DomainTitle: "Onboarding do Prestador",
                ResourceLabel: "onboarding do prestador",
                BusinessContext: "Garante que o prestador conclua requisitos minimos antes de operar na plataforma.",
                TechnicalContext: "Controla status e bloqueios de acesso a endpoints de producao.",
                Audience: "Prestador/Operacao",
                Rules:
                [
                    "Nao liberar operacao sem onboarding completo.",
                    "Registrar claramente pendencias obrigatorias.",
                    "Sincronizar status com validacoes de perfil e plano."
                ]),
            "Reviews" => new CatalogEntry(
                DomainTitle: "Avaliacoes",
                ResourceLabel: "avaliacoes e reputacao",
                BusinessContext: "Coleta e consulta feedbacks para suporte a confianca e qualidade no marketplace.",
                TechnicalContext: "Manipula notas/comentarios com regras de elegibilidade por atendimento.",
                Audience: "Cliente/Prestador/Admin",
                Rules:
                [
                    "Avaliacoes devem estar vinculadas a atendimento valido.",
                    "Aplicar moderacao quando necessario para compliance.",
                    "Preservar historico para analise de reputacao."
                ]),
            "Routes" => new CatalogEntry(
                DomainTitle: "Rotas e Geolocalizacao",
                ResourceLabel: "calculo de rotas e distancia",
                BusinessContext: "Suporta decisoes de cobertura, deslocamento e elegibilidade operacional.",
                TechnicalContext: "Consulta servicos de geocodificacao/roteamento com fallback e timeout.",
                Audience: "Apps/Portais/Operacao",
                Rules:
                [
                    "Tratar indisponibilidade de provedor de mapa com fallback.",
                    "Normalizar coordenadas e unidades de distancia.",
                    "Nao depender de resposta sem validacao de consistencia."
                ]),
            "ServiceAppointments" or "ServiceAppointmentEvidences" => new CatalogEntry(
                DomainTitle: "Atendimentos",
                ResourceLabel: "agendamentos e evidencias do atendimento",
                BusinessContext: "Gerencia ciclo do atendimento: agenda, execucao, disputa e garantia.",
                TechnicalContext: "Controla transicoes de estado sensiveis e integra com provas operacionais.",
                Audience: "Cliente/Prestador/Admin/Suporte",
                Rules:
                [
                    "Transicoes de estado devem obedecer maquina de estados do atendimento.",
                    "Conflitos de horario/slot devem retornar erro claro para replanejamento.",
                    "Evidencias e disputas precisam manter trilha temporal integra."
                ]),
            "ServiceCategories" => new CatalogEntry(
                DomainTitle: "Catalogo de Servicos",
                ResourceLabel: "categorias de servico",
                BusinessContext: "Organiza o catalogo de servicos para descoberta e classificacao de pedidos.",
                TechnicalContext: "Fornece referencias usadas por web/mobile/admin e filtros analiticos.",
                Audience: "Cliente/Prestador/Admin",
                Rules:
                [
                    "Manter nomes/slugs consistentes para nao quebrar filtros.",
                    "Alteracoes de status devem preservar pedidos existentes.",
                    "Evitar duplicidade sem controle de chave logica."
                ]),
            "ServiceRequests" => new CatalogEntry(
                DomainTitle: "Pedidos de Servico",
                ResourceLabel: "pedidos de servico",
                BusinessContext: "Cobre abertura, consulta e evolucao de pedidos no marketplace.",
                TechnicalContext: "Integra com propostas, chats, agenda e monitoramento operacional.",
                Audience: "Cliente/Prestador/Admin",
                Rules:
                [
                    "Validar dados obrigatorios de localizacao/categoria/descricao.",
                    "Sincronizar mudancas de status com propostas e agenda.",
                    "Priorizar idempotencia em operacoes criticas de transicao."
                ]),
            "LegalTerms" => new CatalogEntry(
                DomainTitle: "Termos Legais",
                ResourceLabel: "versionamento e aceite de termos",
                BusinessContext: "Controla publicacao e aceite de termos por publico da plataforma.",
                TechnicalContext: "Mantem historico versionado e trilha de consentimento auditavel.",
                Audience: "Admin/Cliente/Prestador/Compliance",
                Rules:
                [
                    "Nao sobrescrever versao historica de termo publicado.",
                    "Aceite deve guardar usuario, versao e timestamp UTC.",
                    "Disponibilizar conteudo ativo para leitura antes da acao de aceite."
                ]),
            _ => BuildFallbackContext("API", BuildResourceLabel(controller, null))
        };
    }

    private static CatalogEntry BuildFallbackContext(string domain, string resourceLabel)
    {
        return new CatalogEntry(
            DomainTitle: domain,
            ResourceLabel: resourceLabel,
            BusinessContext: "Opera fluxos de negocio do ecossistema ConsertaPraMim.",
            TechnicalContext: "Endpoint REST documentado para consumo web/mobile/admin.",
            Audience: "Times de Produto/QA/Integracao",
            Rules:
            [
                "Validar contrato antes de consumir em producao.",
                "Tratar respostas de erro com fallback no cliente.",
                "Correlacionar chamadas com telemetria da API."
            ]);
    }

    private static string BuildResourceLabel(string controller, string? fallbackPrefix)
    {
        if (string.IsNullOrWhiteSpace(controller))
        {
            return "recursos da API";
        }

        var sanitized = controller
            .Replace("Controller", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Admin", " Admin ", StringComparison.OrdinalIgnoreCase)
            .Replace("MobileClient", " Cliente Mobile ", StringComparison.OrdinalIgnoreCase)
            .Replace("MobileProvider", " Prestador Mobile ", StringComparison.OrdinalIgnoreCase)
            .Replace("MobileAdmin", " Admin Mobile ", StringComparison.OrdinalIgnoreCase);

        var words = SplitPascalCase(sanitized)
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(words))
        {
            return string.IsNullOrWhiteSpace(fallbackPrefix) ? "recursos da API" : fallbackPrefix;
        }

        return words;
    }

    private static string SplitPascalCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var buffer = new System.Text.StringBuilder();
        for (var index = 0; index < input.Length; index++)
        {
            var current = input[index];
            if (index > 0 &&
                char.IsUpper(current) &&
                !char.IsUpper(input[index - 1]) &&
                input[index - 1] != ' ')
            {
                buffer.Append(' ');
            }

            buffer.Append(char.ToLowerInvariant(current));
        }

        return buffer.ToString();
    }
}
