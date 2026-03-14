using AppMobileCPM.Models;

namespace AppMobileCPM.Services;

public sealed class InMemoryMarketplaceRepository : IMarketplaceRepository
{
    private readonly object _writeLock = new();
    private readonly List<ServiceRequest> _serviceRequests = [];
    private readonly List<ProfessionalRegistration> _professionalRegistrations = [];
    private readonly List<SupportRequest> _supportRequests = [];

    private readonly List<ServiceCategory> _categories =
    [
        new() { Id = "eletricista", Name = "Eletricista", IconClass = "bi-lightning-charge-fill" },
        new() { Id = "encanador", Name = "Encanador", IconClass = "bi-droplet-fill" },
        new() { Id = "pedreiro", Name = "Pedreiro", IconClass = "bi-bricks" },
        new() { Id = "pintor", Name = "Pintor", IconClass = "bi-brush-fill" },
        new() { Id = "chaveiro", Name = "Chaveiro", IconClass = "bi-key-fill" },
        new() { Id = "ar-condicionado", Name = "Ar Condicionado", IconClass = "bi-snow" },
        new() { Id = "montador", Name = "Montador de Moveis", IconClass = "bi-tools" },
        new() { Id = "eletrodomesticos", Name = "Tecnico de Eletrodomesticos", IconClass = "bi-wrench-adjustable-circle-fill" },
        new() { Id = "telhado", Name = "Telhado", IconClass = "bi-house-fill" },
        new() { Id = "reformas", Name = "Reformas", IconClass = "bi-hammer" }
    ];

    private readonly List<string> _professionOptions =
    [
        "Ajudante Geral de Obras",
        "Aplicador de Massa Corrida",
        "Aplicador de Resina em Piso",
        "Arquiteto",
        "Assentador de Piso Frio",
        "Assentador de Porcelanato",
        "Azulejista",
        "Bombeiro Hidraulico",
        "Calheiro",
        "Carpinteiro",
        "Chaveiro Residencial",
        "Consultor de Reformas",
        "Controlador de Pragas",
        "Dedetizador",
        "Desentupidor",
        "Designer de Interiores",
        "Eletricista",
        "Eletricista de Manutencao",
        "Eletricista Predial",
        "Eletricista Residencial",
        "Encanador",
        "Engenheiro Civil",
        "Gesseiro",
        "Funileiro de Telhado",
        "Higienizador de Ar Condicionado",
        "Impermeabilizador",
        "Instalador de Alarmes",
        "Instalador de Ar Condicionado",
        "Instalador de Box",
        "Instalador de Cameras",
        "Instalador de Cerca Eletrica",
        "Instalador de Drywall",
        "Instalador de Energia Solar",
        "Instalador de Espelhos",
        "Instalador de Forro PVC",
        "Instalador de Gas",
        "Instalador de Interfone",
        "Instalador de Janela",
        "Instalador de Papel de Parede",
        "Instalador de Piso Laminado",
        "Instalador de Piso Vinilico",
        "Instalador de Porta e Fechadura",
        "Instalador de Portao Eletronico",
        "Instalador de Porteiro Eletronico",
        "Instalador de Rede de Protecao",
        "Instalador de Rodape",
        "Jardinagem e Poda",
        "Jardineiro",
        "Limpador de Caixa D Agua",
        "Limpeza Pos-Obra",
        "Lustrador de Piso",
        "Marceneiro",
        "Marido de Aluguel",
        "Mecanico de Bomba Hidraulica",
        "Mestre de Obras",
        "Montador de Moveis",
        "Paisagista",
        "Pedreiro",
        "Pintor Predial",
        "Pintor Residencial",
        "Pintor de Fachada",
        "Piscineiro",
        "Reparador de Persianas",
        "Reparador de Portao Eletronico",
        "Revestidor de Parede",
        "Rufeiro",
        "Serralheiro",
        "Sintequeiro",
        "Soldador",
        "Tecnico de Ar Condicionado",
        "Tecnico de Aquecedor a Gas",
        "Tecnico de Aquecedor Solar",
        "Tecnico de Bomba de Piscina",
        "Tecnico de CFTV",
        "Tecnico de Eletrodomesticos",
        "Tecnico de Fogao",
        "Tecnico de Forno Eletrico",
        "Tecnico de Freezer",
        "Tecnico de Geladeira",
        "Tecnico de Lava e Seca",
        "Tecnico de Lava-Loucas",
        "Tecnico de Maquina de Lavar",
        "Tecnico de Maquina de Secar",
        "Tecnico de Microondas",
        "Tecnico de Porta de Vidro",
        "Tecnico de Portao Eletronico",
        "Tecnico de Pressurizador",
        "Tecnico de Purificador de Agua",
        "Tecnico de Refrigerador Expositor",
        "Tecnico de Refrigeracao",
        "Tecnico de Ventilador de Teto",
        "Tecnico de TV",
        "Telhadista",
        "Texturizador",
        "Vidraceiro"
    ];

    private readonly List<Professional> _professionals =
    [
        new()
        {
            Id = 1,
            Name = "Joao Silva",
            Profession = "Eletricista",
            Description = "Eletricista residencial para instalacoes, reparos e manutencao preventiva.",
            Rating = 4.9,
            Reviews = 128,
            Distance = "2.5 km",
            Services = ["Instalacao eletrica", "Reparos", "Projetos"],
            ServicePhotoUrls =
            [
                "https://picsum.photos/seed/joao-servico-1/420/280",
                "https://picsum.photos/seed/joao-servico-2/420/280",
                "https://picsum.photos/seed/joao-servico-3/420/280",
                "https://picsum.photos/seed/joao-servico-4/420/280"
            ],
            Verified = true,
            ImageUrl = "https://picsum.photos/seed/joao/200/200",
            WhatsappUrl = "https://wa.me/5511990000001"
        },
        new()
        {
            Id = 2,
            Name = "Maria Oliveira",
            Profession = "Encanadora",
            Description = "Especialista em vazamentos, desentupimento e instalacoes hidraulicas.",
            Rating = 4.8,
            Reviews = 95,
            Distance = "3.1 km",
            Services = ["Vazamentos", "Desentupimento", "Instalacoes"],
            ServicePhotoUrls =
            [
                "https://picsum.photos/seed/maria-servico-1/420/280",
                "https://picsum.photos/seed/maria-servico-2/420/280",
                "https://picsum.photos/seed/maria-servico-3/420/280",
                "https://picsum.photos/seed/maria-servico-4/420/280"
            ],
            Verified = true,
            ImageUrl = "https://picsum.photos/seed/maria/200/200",
            WhatsappUrl = "https://wa.me/5511990000002"
        },
        new()
        {
            Id = 3,
            Name = "Carlos Santos",
            Profession = "Pedreiro",
            Description = "Pedreiro para reformas, alvenaria e acabamento com foco em qualidade.",
            Rating = 4.7,
            Reviews = 210,
            Distance = "5.0 km",
            Services = ["Reformas", "Alvenaria", "Acabamento"],
            ServicePhotoUrls =
            [
                "https://picsum.photos/seed/carlos-servico-1/420/280",
                "https://picsum.photos/seed/carlos-servico-2/420/280",
                "https://picsum.photos/seed/carlos-servico-3/420/280",
                "https://picsum.photos/seed/carlos-servico-4/420/280"
            ],
            Verified = false,
            ImageUrl = "https://picsum.photos/seed/carlos/200/200",
            WhatsappUrl = "https://wa.me/5511990000003"
        },
        new()
        {
            Id = 4,
            Name = "Ana Costa",
            Profession = "Pintora",
            Description = "Pintora residencial para pintura interna, texturas e massa corrida.",
            Rating = 5.0,
            Reviews = 42,
            Distance = "1.2 km",
            Services = ["Pintura interna", "Texturas", "Massa corrida"],
            ServicePhotoUrls =
            [
                "https://picsum.photos/seed/ana-servico-1/420/280",
                "https://picsum.photos/seed/ana-servico-2/420/280",
                "https://picsum.photos/seed/ana-servico-3/420/280",
                "https://picsum.photos/seed/ana-servico-4/420/280"
            ],
            Verified = true,
            ImageUrl = "https://picsum.photos/seed/ana/200/200",
            WhatsappUrl = "https://wa.me/5511990000004"
        },
        new()
        {
            Id = 5,
            Name = "Alan Araujo",
            Profession = "Eletricista e Tecnico de Ar Condicionado",
            Description = "Eletricista, instalacao e manutencao de ar-condicionado, alguns reparos em hidraulica.",
            Rating = 4.9,
            Reviews = 67,
            Distance = "4.3 km",
            Services = ["Eletrica residencial", "Ar-condicionado", "Reparos hidraulicos"],
            ServicePhotoUrls =
            [
                "https://picsum.photos/seed/alan-servico-1/420/280",
                "https://picsum.photos/seed/alan-servico-2/420/280",
                "https://picsum.photos/seed/alan-servico-3/420/280",
                "https://picsum.photos/seed/alan-servico-4/420/280"
            ],
            Verified = true,
            ImageUrl = "https://picsum.photos/seed/alan/200/200",
            WhatsappUrl = "https://wa.me/5511990000005"
        }
    ];

    private readonly List<string> _supportCategoryOptions =
    [
        "Duvidas sobre cadastro",
        "Problemas tecnicos na plataforma",
        "Pagamentos e cobrancas",
        "Perfil profissional e reputacao",
        "Cancelamentos e contestacoes",
        "Sugestoes e melhorias",
        "Outros assuntos"
    ];

    private readonly List<FaqItem> _supportFaqItems =
    [
        new() { Question = "Como recebo novos pedidos de servico?", Answer = "Mantenha seu perfil atualizado, com profissao principal, servicos realizados e telefone valido. Quando houver demanda na sua regiao, os contatos podem ser enviados para o numero cadastrado." },
        new() { Question = "Preciso pagar para me cadastrar como profissional?", Answer = "O cadastro inicial e gratuito. A plataforma pode oferecer planos e recursos adicionais, que sao informados separadamente antes de qualquer contratacao." },
        new() { Question = "Quanto tempo leva para analisar meu cadastro?", Answer = "A validacao inicial normalmente ocorre em ate 48 horas uteis. Em periodos de alta demanda esse prazo pode variar." },
        new() { Question = "Posso alterar minha profissao e meus servicos depois?", Answer = "Sim. Voce pode ajustar suas informacoes de perfil sempre que necessario para refletir melhor sua area de atuacao." },
        new() { Question = "Como funciona a avaliacao dos clientes?", Answer = "Clientes podem avaliar atendimentos apos a conclusao do servico. As avaliacoes ajudam outros usuarios e impactam a visibilidade do seu perfil." },
        new() { Question = "O que acontece se um cliente cancelar o servico?", Answer = "Cancelamentos devem ser tratados com transparencia entre as partes. Em casos de conflito, registre detalhes no suporte para analise." },
        new() { Question = "Como atualizo meu telefone ou CEP?", Answer = "Acesse a area de cadastro/perfil e atualize os dados. Sempre mantenha telefone e CEP corretos para evitar perda de oportunidades." },
        new() { Question = "Como reportar comportamento inadequado de cliente ou profissional?", Answer = "Use o formulario de suporte e selecione a categoria adequada. Informe data, contexto e evidencias para agilizar a apuracao." },
        new() { Question = "Posso atender em mais de uma cidade?", Answer = "Sim, desde que voce consiga cumprir prazos e deslocamento. Recomenda-se detalhar no seu perfil sua area principal de atendimento." },
        new() { Question = "Como faco para encerrar meu cadastro?", Answer = "Envie uma solicitacao no suporte com o assunto de encerramento de conta. A equipe confirmara os passos e prazos." }
    ];

    private readonly Dictionary<string, string> _siteContents = new(StringComparer.OrdinalIgnoreCase)
    {
        ["home.hero.title"] = "Resolva os problemas da sua casa",
        ["home.hero.subtitle"] = "Encontre eletricistas, encanadores, pedreiros e outros especialistas em minutos, com atendimento proximo de voce.",
        ["home.topservices.title"] = "Servicos mais buscados",
        ["home.topservices.subtitle"] = "Escolha a categoria e receba orcamentos rapidamente",
        ["home.about.title"] = "Sobre a ConsertaPraMim",
        ["home.about.subtitle"] = "Somos uma plataforma digital que conecta clientes e prestadores locais para servicos domesticos, manutencao, instalacoes e reformas.",
        ["home.about.proposal.title"] = "Nossa proposta",
        ["home.about.proposal.paragraph1"] = "A ConsertaPraMim nasceu para simplificar a contratacao de servicos e fortalecer profissionais da propria regiao. A operacao inicial comeca em Praia Grande, com foco em qualidade, organizacao e comunicacao clara.",
        ["home.about.proposal.paragraph2"] = "Aqui, cliente e profissional conversam de forma direta, recebem e enviam orcamentos com agilidade e acompanham o atendimento com mais transparencia.",
        ["home.about.differentials.title"] = "Diferenciais da plataforma",
        ["home.howitworks.title"] = "Como funciona",
        ["home.howitworks.subtitle"] = "Fluxo completo da solicitacao ate a avaliacao final",
        ["home.procta.title"] = "Voce e profissional?",
        ["home.procta.subtitle"] = "Cadastre-se no ConsertaPraMim e receba novas oportunidades todos os dias na sua cidade.",
        ["home.finalcta.title"] = "Pronto para resolver seu problema?",
        ["home.finalcta.subtitle"] = "Solicite agora e receba contatos de profissionais proximos.",
        ["legal.professional.html"] = "<p><strong>Ultima atualizacao:</strong> 12 de marco de 2026.</p><h5>1. Aceitacao</h5><p>Ao se cadastrar, o profissional concorda com estes termos e com as politicas da plataforma.</p><h5>2. Cadastro</h5><p>As informacoes devem ser verdadeiras e atualizadas, incluindo nome, telefone, CEP e servicos prestados.</p><h5>3. Responsabilidades</h5><p>O profissional e responsavel pela execucao tecnica, qualidade e conduta no atendimento.</p><h5>4. Relacao com clientes</h5><p>Valores, prazos e condicoes sao negociados diretamente entre as partes.</p><h5>5. Conduta</h5><p>Fraudes, linguagem ofensiva ou praticas abusivas podem gerar suspensao da conta.</p><h5>6. Contato</h5><p>Em caso de duvidas, utilize a Central de Suporte.</p>",
        ["legal.privacy.html"] = "<p><strong>Ultima atualizacao:</strong> 12 de marco de 2026.</p><h5>1. Coleta de dados</h5><p>Coletamos dados como nome, telefone, e-mail e CEP para operar a plataforma.</p><h5>2. Uso dos dados</h5><p>Utilizamos os dados para conectar clientes e profissionais, suporte e melhoria continua.</p><h5>3. Compartilhamento</h5><p>Compartilhamos apenas o necessario para viabilizar a prestacao do servico.</p><h5>4. Seguranca</h5><p>Adotamos medidas tecnicas e administrativas para proteger as informacoes.</p><h5>5. Direitos do titular</h5><p>Voce pode solicitar acesso, correcao e exclusao de dados conforme a legislacao aplicavel.</p>"
    };

    public InMemoryMarketplaceRepository()
    {
        SeedAdditionalSiteContents();
    }

    private void SeedAdditionalSiteContents()
    {
        var additionalContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["branding.author"] = "ConsertaPraMim",
            ["branding.default_description"] = "ConsertaPraMim conecta voce a profissionais para servicos de eletrica, hidraulica, reformas, pintura e manutencao residencial.",
            ["branding.default_keywords"] = "conserto residencial, manutencao casa, eletricista, encanador, pedreiro, pintor, reformas",
            ["branding.default_robots"] = "index,follow,max-image-preview:large,max-snippet:-1,max-video-preview:-1",
            ["branding.favicon_url"] = "/images/logo-top-bar-consertapramim.png",
            ["branding.logo_alt"] = "ConsertaPraMim",
            ["branding.logo_url"] = "/images/logo-top-bar-consertapramim.png",
            ["branding.og_image_alt"] = "ConsertaPraMim",
            ["branding.og_image_url"] = "/images/7237a130-fa5b-4e98-9118-ed5b0d465fa6.png",
            ["branding.site_name"] = "ConsertaPraMim",
            ["branding.theme_color"] = "#ff7a00",
            ["error.meta.description"] = "Ocorreu um erro ao processar sua solicitacao no ConsertaPraMim.",
            ["error.meta.title"] = "Erro",
            ["error.page.home_button"] = "Voltar para a pagina inicial",
            ["error.page.request_id_label"] = "Request ID:",
            ["error.page.subtitle"] = "Tente novamente em alguns instantes. Se o problema persistir, entre em contato com o suporte.",
            ["error.page.support_button"] = "Falar com suporte",
            ["error.page.title"] = "Erro ao processar a solicitacao",
            ["home.about.differentials.item1"] = "Prestadores locais",
            ["home.about.differentials.item2"] = "Comunicacao direta",
            ["home.about.differentials.item3"] = "Orcamento rapido",
            ["home.about.differentials.item4"] = "Agendamento simples",
            ["home.about.differentials.item5"] = "Contato via WhatsApp",
            ["home.about.differentials.item6"] = "Avaliacao mutua",
            ["home.about.differentials.item7"] = "Painel de acompanhamento",
            ["home.about.differentials.item8"] = "Equipe de suporte",
            ["home.about.differentials.title"] = "Diferenciais da plataforma",
            ["home.about.proposal.paragraph1"] = "A ConsertaPraMim nasceu para simplificar a contratacao de servicos e fortalecer profissionais da propria regiao. A operacao inicial comeca em Praia Grande, com foco em qualidade, organizacao e comunicacao clara.",
            ["home.about.proposal.paragraph2"] = "Aqui, cliente e profissional conversam de forma direta, recebem e enviam orcamentos com agilidade e acompanham o atendimento com mais transparencia.",
            ["home.about.proposal.title"] = "Nossa proposta",
            ["home.about.subtitle"] = "Somos uma plataforma digital que conecta clientes e prestadores locais para servicos domesticos, manutencao, instalacoes e reformas.",
            ["home.about.title"] = "Sobre a ConsertaPraMim",
            ["home.finalcta.button"] = "Solicitar servico agora",
            ["home.finalcta.subtitle"] = "Solicite agora e receba contatos de profissionais proximos.",
            ["home.finalcta.title"] = "Pronto para resolver seu problema?",
            ["home.hero.cep.loading"] = "Consultando CEP...",
            ["home.hero.cep.resolved_template"] = "Endereco do CEP: {address}",
            ["home.hero.cep.unresolved"] = "Nao foi possivel resolver rua, bairro, cidade e UF. O CEP digitado sera aceito.",
            ["home.hero.form.description_placeholder"] = "Descreva o servico desejado",
            ["home.hero.form.location_button_aria"] = "Usar minha localizacao",
            ["home.hero.form.location_button_title"] = "Usar minha localizacao",
            ["home.hero.form.location_placeholder"] = "Digite seu CEP",
            ["home.hero.form.submit"] = "Encontrar profissionais",
            ["home.hero.location.cep_not_identified"] = "Nao foi possivel identificar o CEP automaticamente.",
            ["home.hero.location.denied"] = "Permissao negada para acessar sua localizacao.",
            ["home.hero.location.generic_error"] = "Nao foi possivel obter sua localizacao.",
            ["home.hero.location.loading"] = "Obtendo sua localizacao...",
            ["home.hero.location.reverse_failed"] = "Nao foi possivel converter sua localizacao em CEP.",
            ["home.hero.location.timeout"] = "Tempo excedido ao buscar sua localizacao.",
            ["home.hero.location.unavailable"] = "Localizacao indisponivel no momento.",
            ["home.hero.location.unsupported"] = "Seu navegador nao suporta geolocalizacao.",
            ["home.hero.subtitle"] = "Encontre eletricistas, encanadores, pedreiros e outros especialistas em minutos, com atendimento proximo de voce.",
            ["home.hero.title"] = "Resolva os problemas da sua casa",
            ["home.howitworks.step1.description"] = "O cliente descreve o problema e informa a localizacao.",
            ["home.howitworks.step1.title"] = "Cliente solicita o servico",
            ["home.howitworks.step2.description"] = "Profissionais da regiao visualizam a oportunidade de atendimento.",
            ["home.howitworks.step2.title"] = "Prestadores recebem o pedido",
            ["home.howitworks.step3.description"] = "Cada prestador interessado pode enviar sua proposta.",
            ["home.howitworks.step3.title"] = "Profissional envia orcamento",
            ["home.howitworks.step4.description"] = "As partes alinham detalhes, prazo, valor e agendamento.",
            ["home.howitworks.step4.title"] = "Cliente conversa diretamente",
            ["home.howitworks.step5.description"] = "O profissional executa o trabalho conforme o combinado.",
            ["home.howitworks.step5.title"] = "Servico e realizado",
            ["home.howitworks.step6.description"] = "As avaliacoes ajudam a manter confianca e qualidade na plataforma.",
            ["home.howitworks.step6.title"] = "Ambos avaliam o atendimento",
            ["home.howitworks.subtitle"] = "Fluxo completo da solicitacao ate a avaliacao final",
            ["home.howitworks.title"] = "Como funciona",
            ["home.meta.description"] = "Solicite servicos residenciais no ConsertaPraMim e encontre eletricista, encanador, pintor e outros profissionais perto de voce.",
            ["home.meta.keywords"] = "servicos residenciais, eletricista, encanador, pedreiro, pintor, conserto casa",
            ["home.meta.og_image_url"] = "/images/7237a130-fa5b-4e98-9118-ed5b0d465fa6.png",
            ["home.meta.seo_title"] = "Resolva os problemas da sua casa",
            ["home.meta.title"] = "Resolva os problemas da sua casa",
            ["home.procta.button_primary"] = "Quero me cadastrar",
            ["home.procta.button_secondary"] = "Saiba mais",
            ["home.procta.image_alt"] = "Profissional trabalhando",
            ["home.procta.image_url"] = "/images/7237a130-fa5b-4e98-9118-ed5b0d465fa6.png",
            ["home.procta.subtitle"] = "Cadastre-se no ConsertaPraMim e receba novas oportunidades todos os dias na sua cidade.",
            ["home.procta.title"] = "Voce e profissional?",
            ["home.schema.howto.name"] = "Como funciona a plataforma ConsertaPraMim",
            ["home.schema.howto.step1.name"] = "Cliente solicita o servico",
            ["home.schema.howto.step1.text"] = "Descreva o problema e informe seu CEP.",
            ["home.schema.howto.step2.name"] = "Prestadores recebem o pedido",
            ["home.schema.howto.step2.text"] = "Profissionais da regiao visualizam sua solicitacao.",
            ["home.schema.howto.step3.name"] = "Profissional envia orcamento",
            ["home.schema.howto.step3.text"] = "Os interessados retornam com proposta e prazo.",
            ["home.schema.howto.step4.name"] = "Cliente conversa e fecha",
            ["home.schema.howto.step4.text"] = "Cliente e profissional alinham valor e agendamento.",
            ["home.schema.howto.step5.name"] = "Servico concluido e avaliado",
            ["home.schema.howto.step5.text"] = "Apos o atendimento, ambos podem avaliar a experiencia.",
            ["home.schema.itemlist.name"] = "Servicos mais buscados",
            ["home.schema.webpage.description"] = "Solicite servicos residenciais no ConsertaPraMim e encontre profissionais perto de voce.",
            ["home.schema.webpage.name"] = "Resolva os problemas da sua casa",
            ["home.topservices.subtitle"] = "Escolha a categoria e receba orcamentos rapidamente",
            ["home.topservices.title"] = "Servicos mais buscados",
            ["home.trust.card1.description"] = "Analisamos dados e documentos antes da ativacao no portal.",
            ["home.trust.card1.title"] = "Profissionais verificados",
            ["home.trust.card2.description"] = "Consulte opinioes de clientes para escolher com mais seguranca.",
            ["home.trust.card2.title"] = "Avaliacoes reais",
            ["home.trust.card3.description"] = "Nossa equipe acompanha voce durante toda a jornada.",
            ["home.trust.card3.title"] = "Suporte humano",
            ["layout.footer.about"] = "Conectando clientes a profissionais verificados para resolver problemas da casa de forma rapida e segura.",
            ["layout.footer.contact.help_center"] = "Central de ajuda",
            ["layout.footer.contact.support"] = "Falar com suporte",
            ["layout.footer.contact.title"] = "Contato",
            ["layout.footer.copyright"] = "{year} {siteName}. Todos os direitos reservados.",
            ["layout.footer.platform.about"] = "Sobre",
            ["layout.footer.platform.how_it_works"] = "Como funciona",
            ["layout.footer.platform.professionals"] = "Profissionais",
            ["layout.footer.platform.title"] = "Plataforma",
            ["layout.footer.privacy"] = "Privacidade",
            ["layout.footer.professionals.plans"] = "Planos",
            ["layout.footer.professionals.register"] = "Cadastre-se",
            ["layout.footer.professionals.title"] = "Profissionais",
            ["layout.footer.terms"] = "Termos de uso",
            ["layout.nav.home"] = "Como funciona",
            ["layout.nav.professional_cta"] = "Sou Profissional",
            ["layout.nav.professionals"] = "Profissionais",
            ["layout.nav.support"] = "Suporte",
            ["layout.nav.toggle_aria_label"] = "Alternar menu",
            ["layout.schema.area_served"] = "Brasil",
            ["layout.schema.service_type"] = "Intermediacao de servicos residenciais",
            ["privacy.meta.description"] = "Entenda como o ConsertaPraMim coleta, utiliza e protege dados pessoais de clientes e profissionais.",
            ["privacy.meta.keywords"] = "politica de privacidade, LGPD, dados pessoais, ConsertaPraMim",
            ["privacy.meta.seo_title"] = "Politica de privacidade",
            ["privacy.meta.title"] = "Politica de privacidade",
            ["privacy.page.title"] = "Politica de privacidade",
            ["privacy.schema.webpage.description"] = "Politica de privacidade da plataforma ConsertaPraMim.",
            ["privacy.schema.webpage.name"] = "Politica de privacidade",
            ["professionals.card.distance_template"] = "A {distance} de voce",
            ["professionals.card.view_services"] = "Ver servicos",
            ["professionals.empty"] = "Nenhum profissional encontrado para sua busca.",
            ["professionals.gallery.close"] = "Fechar",
            ["professionals.gallery.main_image_alt"] = "Foto do servico",
            ["professionals.gallery.next"] = "Proxima foto",
            ["professionals.gallery.no_photos_alt"] = "Sem fotos de servico",
            ["professionals.gallery.previous"] = "Foto anterior",
            ["professionals.gallery.service_alt_template"] = "Servico {index} de {name}",
            ["professionals.gallery.thumb_alt_template"] = "Miniatura {index} de {name}",
            ["professionals.gallery.title"] = "Galeria de servicos",
            ["professionals.gallery.title_template"] = "Galeria de servicos - {name}",
            ["professionals.meta.description"] = "Busque profissionais por nome ou profissao e encontre especialistas para eletrica, hidraulica, reformas e manutencao residencial.",
            ["professionals.meta.keywords"] = "profissionais, eletricista, encanador, pedreiro, pintor, servicos residenciais",
            ["professionals.meta.og_image_url"] = "/images/7237a130-fa5b-4e98-9118-ed5b0d465fa6.png",
            ["professionals.meta.seo_title"] = "Profissionais de servicos residenciais",
            ["professionals.meta.title"] = "Profissionais",
            ["professionals.page.subtitle"] = "Encontre o especialista ideal para o seu problema.",
            ["professionals.page.title"] = "Profissionais disponiveis",
            ["professionals.schema.collection.description"] = "Lista de profissionais disponiveis para servicos residenciais.",
            ["professionals.schema.collection.name"] = "Profissionais disponiveis",
            ["professionals.schema.itemlist.name"] = "Prestadores em destaque",
            ["professionals.search.button"] = "Buscar",
            ["professionals.search.placeholder"] = "Buscar por nome ou profissao",
            ["register.alert.success"] = "Cadastro recebido. Nossa equipe fara contato em ate 48 horas.",
            ["register.form.cep.label"] = "CEP",
            ["register.form.cep.loading"] = "Consultando CEP...",
            ["register.form.cep.placeholder"] = "00000-000",
            ["register.form.cep.resolved_template"] = "Endereco encontrado: {address}",
            ["register.form.cep.unresolved"] = "Nao foi possivel resolver rua, bairro, cidade e UF. O CEP digitado sera aceito.",
            ["register.form.experience.label"] = "Resumo da experiencia",
            ["register.form.experience.placeholder"] = "Conte sobre sua experiencia e tempo de atuacao.",
            ["register.form.name.label"] = "Nome completo",
            ["register.form.name.placeholder"] = "Seu nome",
            ["register.form.phone.label"] = "Telefone / Celular",
            ["register.form.phone.placeholder"] = "(00) 90000-0000",
            ["register.form.photo.subtitle"] = "PNG ou JPG ate 5MB",
            ["register.form.photo.title"] = "Adicionar foto de perfil",
            ["register.form.profession.help"] = "Digite para pesquisar na lista de profissoes.",
            ["register.form.profession.invalid"] = "Selecione uma profissao valida da lista.",
            ["register.form.profession.label"] = "Profissao principal",
            ["register.form.profession.placeholder"] = "Pesquise e selecione sua profissao",
            ["register.form.services.label"] = "Servicos realizados",
            ["register.form.services.placeholder"] = "Ex: Instalacao eletrica, reparos gerais",
            ["register.form.submit"] = "Quero receber mais clientes",
            ["register.form.terms_link"] = "termos de uso para profissionais",
            ["register.form.terms_prefix"] = "Ao se cadastrar, voce concorda com os",
            ["register.form.whatsapp.label"] = "Este numero e WhatsApp",
            ["register.meta.description"] = "Cadastre-se como profissional no ConsertaPraMim para receber pedidos de servicos residenciais na sua regiao.",
            ["register.meta.keywords"] = "cadastro profissional, prestador de servico, receber clientes, servicos residenciais",
            ["register.meta.og_image_url"] = "/images/7237a130-fa5b-4e98-9118-ed5b0d465fa6.png",
            ["register.meta.seo_title"] = "Cadastro de profissional",
            ["register.meta.title"] = "Cadastro profissional",
            ["register.page.subtitle"] = "Cadastre-se gratuitamente e receba pedidos na sua regiao.",
            ["register.page.title"] = "Ganhe clientes com o ConsertaPraMim",
            ["register.schema.webpage.description"] = "Pagina de cadastro para prestadores de servicos da plataforma ConsertaPraMim.",
            ["register.schema.webpage.name"] = "Cadastro de profissional",
            ["register.success.meta.description"] = "Confirmacao de cadastro de profissional no ConsertaPraMim.",
            ["register.success.meta.title"] = "Cadastro realizado com sucesso",
            ["register.success.page.default_name"] = "Profissional",
            ["register.success.page.home_button"] = "Ir para a pagina inicial",
            ["register.success.page.professionals_button"] = "Ver profissionais",
            ["register.success.page.subtitle"] = "Seu perfil foi recebido e em breve voce comecara a receber novas oportunidades de servico.",
            ["register.success.page.title"] = "Cadastro realizado com sucesso",
            ["register.success.page.welcome_template"] = "Seja bem-vindo, <strong>{name}</strong>!",
            ["request.alert.success"] = "Pedido enviado com sucesso. Em breve voce recebera contatos por telefone ou WhatsApp.",
            ["request.form.category.label"] = "Categoria",
            ["request.form.category.option_select"] = "Selecione",
            ["request.form.description.label"] = "Descricao do problema",
            ["request.form.description.placeholder"] = "Ex: A torneira da cozinha esta vazando na base.",
            ["request.form.location.label"] = "Cidade / Bairro",
            ["request.form.location.placeholder"] = "Ex: Sao Paulo - Pinheiros",
            ["request.form.name.label"] = "Nome completo",
            ["request.form.name.placeholder"] = "Seu nome",
            ["request.form.phone.label"] = "Telefone / Celular",
            ["request.form.phone.placeholder"] = "(00) 00000-0000",
            ["request.form.submit"] = "Receber orcamentos",
            ["request.form.terms_text"] = "Ao enviar, voce concorda com os termos de uso e politica de privacidade.",
            ["request.form.whatsapp.label"] = "Este numero e WhatsApp",
            ["request.meta.description"] = "Descreva o servico desejado, informe seu CEP e receba contatos de profissionais da sua regiao.",
            ["request.meta.keywords"] = "solicitar servico, orcamento, manutencao residencial, CEP, profissionais",
            ["request.meta.og_image_url"] = "/images/7237a130-fa5b-4e98-9118-ed5b0d465fa6.png",
            ["request.meta.seo_title"] = "Solicitar servico residencial",
            ["request.meta.title"] = "Solicitar servico",
            ["request.page.subtitle"] = "Preencha os dados para receber contatos de profissionais da sua regiao.",
            ["request.page.title"] = "Solicitar servico",
            ["request.schema.service.area_served"] = "Brasil",
            ["request.schema.service.name"] = "Intermediacao de servicos residenciais",
            ["request.schema.service.provider_name"] = "ConsertaPraMim",
            ["request.schema.webpage.description"] = "Formulario para solicitar atendimento residencial com profissionais proximos.",
            ["request.schema.webpage.name"] = "Solicitar servico",
            ["support.alert.success_default_name"] = "profissional",
            ["support.alert.success_template"] = "Solicitacao recebida com sucesso, {name}! Nossa equipe retornara pelo e-mail informado.",
            ["support.faq.title"] = "FAQ - Perguntas frequentes",
            ["support.form.category.label"] = "Categoria",
            ["support.form.category.option_select"] = "Selecione",
            ["support.form.email.label"] = "E-mail",
            ["support.form.email.placeholder"] = "voce@email.com",
            ["support.form.message.label"] = "Mensagem",
            ["support.form.message.placeholder"] = "Descreva sua solicitacao com o maximo de detalhes possivel.",
            ["support.form.name.label"] = "Nome completo",
            ["support.form.name.placeholder"] = "Seu nome",
            ["support.form.phone.label"] = "Telefone (opcional)",
            ["support.form.phone.placeholder"] = "(00) 90000-0000",
            ["support.form.subject.label"] = "Assunto",
            ["support.form.subject.placeholder"] = "Ex: Duvida sobre avaliacao de cliente",
            ["support.form.submit"] = "Enviar solicitacao",
            ["support.form.title"] = "Formulario de suporte",
            ["support.meta.description"] = "Fale com o suporte do ConsertaPraMim e consulte a FAQ com respostas sobre cadastro, pagamentos, perfil e uso da plataforma.",
            ["support.meta.keywords"] = "suporte, central de ajuda, FAQ, duvidas, ConsertaPraMim",
            ["support.meta.seo_title"] = "Central de suporte",
            ["support.meta.title"] = "Suporte",
            ["support.page.subtitle"] = "Envie sua solicitacao para nossa equipe e consulte respostas rapidas na FAQ.",
            ["support.page.title"] = "Central de suporte",
            ["support.schema.webpage.description"] = "Central de suporte com formulario e perguntas frequentes.",
            ["support.schema.webpage.name"] = "Central de suporte",
            ["terms.meta.description"] = "Consulte os termos de uso para profissionais cadastrados na plataforma ConsertaPraMim.",
            ["terms.meta.keywords"] = "termos de uso, profissionais, plataforma, ConsertaPraMim",
            ["terms.meta.seo_title"] = "Termos de uso para profissionais",
            ["terms.meta.title"] = "Termos de uso para profissionais",
            ["terms.page.title"] = "Termos de uso para profissionais",
            ["terms.schema.webpage.description"] = "Termos de uso aplicaveis a profissionais cadastrados no ConsertaPraMim.",
            ["terms.schema.webpage.name"] = "Termos de uso para profissionais"
        };

        foreach (var item in additionalContents)
        {
            _siteContents.TryAdd(item.Key, item.Value);
        }
    }

    public IReadOnlyList<ServiceCategory> GetCategories() => _categories;

    public ServiceCategory? GetCategoryById(string categoryId) =>
        _categories.FirstOrDefault(c => c.Id.Equals(categoryId, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<string> GetProfessionOptions() => _professionOptions;

    public IReadOnlyList<string> GetSupportCategoryOptions() => _supportCategoryOptions;

    public IReadOnlyList<FaqItem> GetSupportFaqItems() => _supportFaqItems;

    public IReadOnlyDictionary<string, string> GetSiteContents() => _siteContents;

    public string? GetSiteContent(string key) =>
        _siteContents.TryGetValue(key, out var value) ? value : null;

    public IReadOnlyList<Professional> GetProfessionals(string? searchTerm = null)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return _professionals;
        }

        return _professionals
            .Where(p =>
                p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                p.Profession.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                p.Services.Any(s => s.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public void AddServiceRequest(ServiceRequest request)
    {
        lock (_writeLock)
        {
            var nextId = _serviceRequests.Count + 1;
            _serviceRequests.Add(new ServiceRequest
            {
                Id = nextId,
                CategoryId = request.CategoryId,
                CategoryName = request.CategoryName,
                Description = request.Description,
                Location = request.Location,
                Name = request.Name,
                Phone = request.Phone,
                IsWhatsapp = request.IsWhatsapp,
                SubmittedAt = request.SubmittedAt
            });
        }
    }

    public void AddProfessionalRegistration(ProfessionalRegistration registration)
    {
        lock (_writeLock)
        {
            var nextId = _professionalRegistrations.Count + 1;
            _professionalRegistrations.Add(new ProfessionalRegistration
            {
                Id = nextId,
                Name = registration.Name,
                Profession = registration.Profession,
                Services = registration.Services,
                PostalCode = registration.PostalCode,
                Phone = registration.Phone,
                IsWhatsapp = registration.IsWhatsapp,
                Experience = registration.Experience,
                SubmittedAt = registration.SubmittedAt
            });
        }
    }

    public void AddSupportRequest(SupportRequest request)
    {
        lock (_writeLock)
        {
            var nextId = _supportRequests.Count + 1;
            _supportRequests.Add(new SupportRequest
            {
                Id = nextId,
                Name = request.Name,
                Email = request.Email,
                Phone = request.Phone,
                Category = request.Category,
                Subject = request.Subject,
                Message = request.Message,
                SubmittedAt = request.SubmittedAt
            });
        }
    }
}
