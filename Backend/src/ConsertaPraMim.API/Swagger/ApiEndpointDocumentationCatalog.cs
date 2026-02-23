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
