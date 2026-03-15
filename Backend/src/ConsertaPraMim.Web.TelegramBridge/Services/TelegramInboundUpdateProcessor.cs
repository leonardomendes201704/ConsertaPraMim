using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Security;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramInboundUpdateProcessor : ITelegramInboundUpdateProcessor
{
    private const string ClientsBoardType = "clientes";
    private const string ProvidersBoardType = "prestadores";
    private static readonly Regex EmailRegex = new(
        @"(?<![\w.+-])[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}(?![\w.\-])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(
        @"(?<!\d)(?:\+?\d[\d\-\s().]{7,}\d)(?!\d)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PostalCodeRegex = new(
        @"(?<!\d)\d{5}-?\d{3}(?!\d)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LocationPatternRegex = new(
        @"\b(?:moro em|sou de|aqui em|estou em|na cidade de|cidade de|atendo em|trabalho em|atuo em|regiao de|regiao do|regiao da|regiao|em)\s+([\p{L}][\p{L}'\/\- ]{2,80})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly string[] ProviderBoardKeywords =
    [
        "prestador",
        "prestadora",
        "prestadores",
        "cadastro de prestador",
        "quero me cadastrar",
        "quero trabalhar",
        "quero ser parceiro",
        "sou parceiro",
        "sou prestador",
        "sou prestadora",
        "sou tecnico",
        "sou tecnica",
        "trabalho com manutencao",
        "trabalho com conserto",
        "quero atender",
        "quero receber chamados",
        "quero receber leads",
        "ofereco servicos"
    ];
    private static readonly string[] ProviderProfessionKeywords =
    [
        "eletricista",
        "encanador",
        "tecnico",
        "tecnica",
        "marceneiro",
        "marceneira",
        "pintor",
        "pintora",
        "serralheiro",
        "serralheira",
        "chaveiro",
        "dedetizador",
        "dedetizadora",
        "refrigeracao",
        "refrigerista",
        "instalador",
        "instaladora"
    ];
    private static readonly string[] ClientUrgencyKeywords =
    [
        "urgente",
        "urgencia",
        "emergencia",
        "agora",
        "hoje",
        "o quanto antes"
    ];
    private static readonly string[] ClientBudgetKeywords =
    [
        "orcamento",
        "cotacao",
        "quanto custa",
        "preco",
        "valor"
    ];
    private static readonly string[] ClientSchedulingKeywords =
    [
        "agendar",
        "agendamento",
        "marcar",
        "visita tecnica",
        "visita"
    ];
    private static readonly string[] ClientQuestionKeywords =
    [
        "duvida",
        "informacao",
        "orientacao",
        "como funciona"
    ];
    private static readonly string[] ProviderRegistrationKeywords =
    [
        "quero me cadastrar",
        "cadastro",
        "quero ser parceiro",
        "sou parceiro",
        "entrar na plataforma"
    ];
    private static readonly string[] ProviderOpportunityKeywords =
    [
        "quero atender",
        "quero receber chamados",
        "quero receber leads",
        "quero pegar servico",
        "captar clientes"
    ];
    private static readonly string[] LocationNoiseTokens =
    [
        "casa",
        "apartamento",
        "predio",
        "condominio",
        "residencia",
        "comercio",
        "loja",
        "bairro",
        "centro",
        "online"
    ];
    private static readonly (string Category, string[] Keywords)[] ServiceCategoryMap =
    [
        ("Ar-condicionado", ["ar condicionado", "ar-condicionado", "split"]),
        ("Geladeira e refrigeracao", ["geladeira", "freezer", "refrigerador", "refrigeracao", "refrigerista"]),
        ("Maquina de lavar", ["maquina de lavar", "lava e seca", "lavadora", "tanquinho"]),
        ("Fogao e forno", ["fogao", "forno", "cooktop"]),
        ("Eletricista", ["eletricista", "chuveiro", "disjuntor", "tomada", "fiacao", "curto", "energia", "luz"]),
        ("Encanador", ["encanador", "hidraulica", "vazamento", "torneira", "cano", "esgoto", "descarga", "registro"]),
        ("Chaveiro", ["chaveiro", "fechadura", "chave"]),
        ("Marcenaria", ["marcenaria", "marceneiro", "armario", "guarda roupa", "movel planejado"]),
        ("Serralheria", ["serralheria", "serralheiro", "portao", "grade", "solda"]),
        ("Pintura", ["pintura", "pintor", "pintora"]),
        ("TV e audio", ["televisao", "tv", "audio", "som", "home theater"]),
        ("Celular e tablet", ["celular", "smartphone", "iphone", "tablet"]),
        ("Computador e notebook", ["computador", "notebook", "pc", "impressora"]),
        ("Dedetizacao", ["dedetizacao", "praga", "cupim", "barata", "formiga"]),
        ("Limpeza", ["limpeza", "diarista", "faxina"])
    ];

    private readonly ITelegramChatService _telegramChatService;
    private readonly TelegramAutomationOptions _automationOptions;
    private readonly ITelegramLeadAutomationClient _telegramLeadAutomationClient;
    private readonly ITelegramMessageAutomationClient _telegramMessageAutomationClient;
    private readonly ITelegramBotApiClient _telegramBotApiClient;
    private readonly ITelegramHumanHandoffStateService _humanHandoffStateService;
    private readonly ITelegramChatbotObservabilityService _observabilityService;
    private readonly ILogger<TelegramInboundUpdateProcessor> _logger;

    public TelegramInboundUpdateProcessor(
        ITelegramChatService telegramChatService,
        IOptions<TelegramAutomationOptions> automationOptions,
        ITelegramLeadAutomationClient telegramLeadAutomationClient,
        ITelegramMessageAutomationClient telegramMessageAutomationClient,
        ITelegramBotApiClient telegramBotApiClient,
        ITelegramHumanHandoffStateService humanHandoffStateService,
        ITelegramChatbotObservabilityService observabilityService,
        ILogger<TelegramInboundUpdateProcessor> logger)
    {
        _telegramChatService = telegramChatService;
        _automationOptions = automationOptions.Value;
        _telegramLeadAutomationClient = telegramLeadAutomationClient;
        _telegramMessageAutomationClient = telegramMessageAutomationClient;
        _telegramBotApiClient = telegramBotApiClient;
        _humanHandoffStateService = humanHandoffStateService;
        _observabilityService = observabilityService;
        _logger = logger;
    }

    public async Task<bool> ProcessAsync(TelegramUpdate update, string source, CancellationToken cancellationToken)
    {
        if (update.Message is null)
        {
            return false;
        }

        try
        {
            var attachmentCount = (update.Message.Photo?.Count ?? 0)
                + (update.Message.Document is null ? 0 : 1)
                + (update.Message.Audio is null ? 0 : 1)
                + (update.Message.Video is null ? 0 : 1)
                + (update.Message.Voice is null ? 0 : 1);

            _observabilityService.RecordInboundMessage(attachmentCount);

            var storedMessage = await _telegramChatService.ReceiveFromTelegramAsync(update.Message, cancellationToken);
            var capturedContact = ResolveCapturedContact(update.Message);
            var bootstrap = await TryBootstrapLeadAsync(update.Message, storedMessage, capturedContact, cancellationToken);
            await TryMirrorInboundMessageAsync(update.Message, storedMessage, bootstrap.ChatbotConversationId, cancellationToken);
            await TrySendAutomaticResponseAsync(update.Message, bootstrap, capturedContact, cancellationToken);
            return storedMessage is not null;
        }
        catch (Exception exception)
        {
            var stageName = string.Equals(source, "webhook", StringComparison.OrdinalIgnoreCase)
                ? "telegram_webhook_update"
                : "telegram_polling_update";

            _observabilityService.RecordIncident(
                stage: stageName,
                errorCode: "telegram_update_processing_failed",
                correlationId: null,
                message: exception.Message);

            _logger.LogWarning(
                exception,
                "Falha ao processar update Telegram {UpdateId} via {Source}",
                update.UpdateId,
                source);

            throw;
        }
    }

    private async Task<TelegramInboundBootstrapResult> TryBootstrapLeadAsync(
        TelegramMessage updateMessage,
        ChatMessageDto? storedMessage,
        TelegramCapturedContact capturedContact,
        CancellationToken cancellationToken)
    {
        var chatId = updateMessage.Chat?.Id ?? 0;
        if (!_automationOptions.Enabled || chatId <= 0)
        {
            return TelegramInboundBootstrapResult.Disabled;
        }

        var chatbotConversationId = BuildDeterministicGuid("telegram-chatbot-conversation", chatId.ToString(CultureInfo.InvariantCulture));
        var userKey = updateMessage.From?.Id > 0
            ? updateMessage.From.Id.ToString(CultureInfo.InvariantCulture)
            : chatId.ToString(CultureInfo.InvariantCulture);
        var userId = BuildDeterministicGuid("telegram-user", userKey);
        var senderName = storedMessage?.SenderDisplayName;
        if (string.IsNullOrWhiteSpace(senderName))
        {
            senderName = ResolveSenderName(updateMessage);
        }

        var messageText = storedMessage?.Text;
        if (string.IsNullOrWhiteSpace(messageText))
        {
            messageText = NormalizeOptionalText(updateMessage.Text) ?? NormalizeOptionalText(updateMessage.Caption) ?? string.Empty;
        }

        var qualification = ResolveLeadQualification(updateMessage, messageText);

        try
        {
            var result = await _telegramLeadAutomationClient.UpsertLeadAsync(
                new TelegramLeadAutomationUpsertRequest
                {
                    BoardType = qualification.BoardType,
                    ChatbotConversationId = chatbotConversationId,
                    ChannelConversationId = chatId.ToString(CultureInfo.InvariantCulture),
                    TelegramChatId = chatId,
                    UserId = userId,
                    UserName = senderName,
                    UserPhone = capturedContact.Phone,
                    UserEmail = capturedContact.Email,
                    ServiceCategory = qualification.ServiceCategory,
                    PostalCode = qualification.PostalCode,
                    City = qualification.City,
                    StatusNote = BuildStatusNote(qualification),
                    InternalNotes = BuildInitialInternalNotes(updateMessage, qualification, messageText),
                    LastContactAtUtc = storedMessage?.SentAtUtc.UtcDateTime ?? ResolveSentAtUtc(updateMessage)
                },
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Bootstrap do lead Telegram falhou para o chat {ChatId}. BoardType={BoardType} StatusCode={StatusCode} Message={Message}",
                    TelegramSecuritySanitizer.MaskChatId(chatId),
                    qualification.BoardType,
                    result.HttpStatusCode,
                    TelegramSecuritySanitizer.SanitizeMessage(result.Message, 300));
            }

            return new TelegramInboundBootstrapResult(
                Enabled: true,
                ChatbotConversationId: chatbotConversationId,
                BoardType: qualification.BoardType,
                LeadCreated: result.Success && result.Created,
                LeadId: result.LeadId,
                Succeeded: result.Success,
                Qualification: qualification);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Falha ao bootstrapar lead Telegram para o chat {ChatId}.",
                TelegramSecuritySanitizer.MaskChatId(chatId));

            return new TelegramInboundBootstrapResult(
                Enabled: true,
                ChatbotConversationId: chatbotConversationId,
                BoardType: qualification.BoardType,
                LeadCreated: false,
                LeadId: 0,
                Succeeded: false,
                Qualification: qualification);
        }
    }

    private async Task TryMirrorInboundMessageAsync(
        TelegramMessage updateMessage,
        ChatMessageDto? storedMessage,
        Guid? chatbotConversationId,
        CancellationToken cancellationToken)
    {
        if (!_automationOptions.Enabled || !_automationOptions.MirrorMessagesEnabled || storedMessage is null)
        {
            return;
        }

        var chatId = updateMessage.Chat?.Id ?? 0;
        if (chatId <= 0 || string.IsNullOrWhiteSpace(storedMessage.Id))
        {
            return;
        }

        try
        {
            await _telegramMessageAutomationClient.MirrorInboundMessageAsync(
                new TelegramInboundMessageAutomationRequest
                {
                    ChatbotConversationId = chatbotConversationId,
                    ChannelConversationId = chatId.ToString(),
                    ChannelMessageId = storedMessage.Id,
                    TelegramChatId = chatId,
                    SenderDisplayName = storedMessage.SenderDisplayName,
                    MessageText = storedMessage.Text ?? string.Empty,
                    SentAtUtc = storedMessage.SentAtUtc.UtcDateTime,
                    Attachments = storedMessage.Attachments
                        .Select(attachment => new TelegramInboundAttachmentDto
                        {
                            FileName = attachment.FileName,
                            MediaKind = attachment.MediaKind,
                            Url = attachment.Url
                        })
                        .ToList()
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Falha ao espelhar mensagem Telegram para o CPM Full. ChatId={ChatId} MessageId={MessageId}",
                TelegramSecuritySanitizer.MaskChatId(chatId),
                storedMessage.Id);
        }
    }

    private async Task TrySendAutomaticResponseAsync(
        TelegramMessage updateMessage,
        TelegramInboundBootstrapResult bootstrap,
        TelegramCapturedContact capturedContact,
        CancellationToken cancellationToken)
    {
        var chatId = updateMessage.Chat?.Id ?? 0;
        if (!bootstrap.Enabled || chatId <= 0 || !_telegramBotApiClient.IsConfigured)
        {
            return;
        }

        if (_humanHandoffStateService.IsActive(chatId))
        {
            return;
        }

        try
        {
            var response = BuildAutomaticResponse(bootstrap, capturedContact);
            if (response is null)
            {
                return;
            }

            await _telegramBotApiClient.SendMessageAsync(
                chatId,
                response.Value.Text,
                [],
                cancellationToken,
                response.Value.Options);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Falha ao enviar resposta automatica do bot Telegram para o chat {ChatId}.",
                TelegramSecuritySanitizer.MaskChatId(chatId));
        }
    }

    private static string BuildInitialInternalNotes(
        TelegramMessage updateMessage,
        TelegramLeadQualification qualification,
        string? messageText)
    {
        var messageSummary = string.IsNullOrWhiteSpace(messageText)
            ? "sem texto legivel"
            : TrimTo(messageText, 400);

        var username = updateMessage.From?.Username;
        var usernameFragment = string.IsNullOrWhiteSpace(username)
            ? string.Empty
            : $" Username: @{username.Trim()}.";

        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(qualification.Intent))
        {
            details.Add($"Intencao identificada: {qualification.Intent}.");
        }

        if (!string.IsNullOrWhiteSpace(qualification.ServiceCategory))
        {
            var categoryLabel = string.Equals(qualification.BoardType, ProvidersBoardType, StringComparison.OrdinalIgnoreCase)
                ? "Categoria tecnica"
                : "Categoria";
            details.Add($"{categoryLabel}: {qualification.ServiceCategory}.");
        }

        if (!string.IsNullOrWhiteSpace(qualification.City))
        {
            var locationLabel = string.Equals(qualification.BoardType, ProvidersBoardType, StringComparison.OrdinalIgnoreCase)
                ? "Regiao identificada"
                : "Cidade identificada";
            details.Add($"{locationLabel}: {qualification.City}.");
        }

        if (!string.IsNullOrWhiteSpace(qualification.PostalCode))
        {
            details.Add($"CEP informado: {qualification.PostalCode}.");
        }

        var detailsFragment = details.Count == 0
            ? " Qualificacao inicial pendente."
            : $" {string.Join(" ", details)}";

        return $"Lead originado automaticamente pelo bot Telegram no board {qualification.BoardType}.{usernameFragment}{detailsFragment} Mensagem inicial: {messageSummary}";
    }

    private static TelegramAutomaticResponse? BuildAutomaticResponse(
        TelegramInboundBootstrapResult bootstrap,
        TelegramCapturedContact capturedContact)
    {
        if ((capturedContact.HasPhone || capturedContact.HasEmail) && !bootstrap.Succeeded)
        {
            return null;
        }

        if (capturedContact.HasPhone)
        {
            var text = BuildQualificationPrompt(bootstrap.Qualification, capturedContact, leadCreatedNow: false);

            return new TelegramAutomaticResponse(
                text,
                new TelegramMessageSendOptions
                {
                    RemoveReplyKeyboard = true
                });
        }

        if (capturedContact.HasEmail)
        {
            return new TelegramAutomaticResponse(
                BuildQualificationPrompt(bootstrap.Qualification, capturedContact, leadCreatedNow: false),
                new TelegramMessageSendOptions
                {
                    RequestContactButton = true,
                    ContactButtonLabel = "Compartilhar telefone"
                });
        }

        if (!bootstrap.LeadCreated)
        {
            return null;
        }

        var acknowledgement = BuildQualificationPrompt(bootstrap.Qualification, capturedContact, leadCreatedNow: true);

        return new TelegramAutomaticResponse(
            acknowledgement,
            new TelegramMessageSendOptions
            {
                RequestContactButton = true,
                ContactButtonLabel = "Compartilhar telefone"
            });
    }

    private static TelegramLeadQualification ResolveLeadQualification(TelegramMessage message, string? messageText)
    {
        var originalText = NormalizeOptionalText(messageText) ??
                           NormalizeOptionalText(message.Text) ??
                           NormalizeOptionalText(message.Caption) ??
                           string.Empty;
        var normalized = NormalizeMessage(originalText) ?? string.Empty;
        var boardType = ResolveBoardType(normalized);
        var serviceCategory = ExtractServiceCategory(normalized);
        var city = ExtractCityOrRegion(originalText);
        var postalCode = ExtractPostalCode(originalText);
        var intent = string.Equals(boardType, ProvidersBoardType, StringComparison.OrdinalIgnoreCase)
            ? ResolveProviderIntent(normalized)
            : ResolveClientIntent(normalized);

        return new TelegramLeadQualification(
            BoardType: boardType,
            ServiceCategory: serviceCategory,
            City: city,
            PostalCode: postalCode,
            Intent: intent);
    }

    private static string ResolveSenderName(TelegramMessage message)
    {
        var fullName = string.Join(
            " ",
            new[]
            {
                message.From?.FirstName?.Trim(),
                message.From?.LastName?.Trim()
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        if (!string.IsNullOrWhiteSpace(message.From?.Username))
        {
            return $"@{message.From.Username.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(message.Chat?.Title))
        {
            return message.Chat.Title.Trim();
        }

        return "Contato Telegram";
    }

    private static TelegramCapturedContact ResolveCapturedContact(TelegramMessage message)
    {
        var phone = ExtractPhone(message);
        var email = ExtractEmail(message);

        return new TelegramCapturedContact(
            Phone: phone ?? string.Empty,
            Email: email ?? string.Empty,
            SharedNativeContact: !string.IsNullOrWhiteSpace(phone) && message.Contact is not null);
    }

    private static string? ExtractPhone(TelegramMessage message)
    {
        if (message.Contact is not null &&
            IsTrustedSharedContact(message.Contact, message) &&
            TryNormalizePhone(message.Contact.PhoneNumber, out var sharedPhone))
        {
            return sharedPhone;
        }

        var candidateText = NormalizeOptionalText(message.Text) ?? NormalizeOptionalText(message.Caption);
        if (string.IsNullOrWhiteSpace(candidateText))
        {
            return null;
        }

        var match = PhoneRegex.Match(candidateText);
        if (!match.Success || !ShouldAcceptTextualPhone(candidateText))
        {
            return null;
        }

        return TryNormalizePhone(match.Value, out var normalizedPhone)
            ? normalizedPhone
            : null;
    }

    private static string? ExtractEmail(TelegramMessage message)
    {
        var candidateText = NormalizeOptionalText(message.Text) ?? NormalizeOptionalText(message.Caption);
        if (string.IsNullOrWhiteSpace(candidateText))
        {
            return null;
        }

        var match = EmailRegex.Match(candidateText);
        if (!match.Success || !ShouldAcceptTextualEmail(candidateText, match.Value))
        {
            return null;
        }

        return match.Value.Trim();
    }

    private static string ResolveBoardType(string normalizedText)
    {
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return ClientsBoardType;
        }

        if (ContainsAny(normalizedText, ProviderBoardKeywords))
        {
            return ProvidersBoardType;
        }

        var hasProviderSelfIntroduction = ProviderProfessionKeywords.Any(keyword =>
            normalizedText.Contains($"sou {keyword}", StringComparison.Ordinal) ||
            normalizedText.Contains($"sou uma {keyword}", StringComparison.Ordinal) ||
            normalizedText.Contains($"sou um {keyword}", StringComparison.Ordinal) ||
            normalizedText.Contains($"trabalho como {keyword}", StringComparison.Ordinal) ||
            normalizedText.Contains($"atuo como {keyword}", StringComparison.Ordinal));

        return hasProviderSelfIntroduction
            ? ProvidersBoardType
            : ClientsBoardType;
    }

    private static string ResolveClientIntent(string normalizedText)
    {
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return "Solicitar atendimento";
        }

        if (ContainsAny(normalizedText, ClientUrgencyKeywords))
        {
            return "Atendimento urgente";
        }

        if (ContainsAny(normalizedText, ClientBudgetKeywords))
        {
            return "Solicitar orcamento";
        }

        if (ContainsAny(normalizedText, ClientSchedulingKeywords))
        {
            return "Agendar atendimento";
        }

        if (ContainsAny(normalizedText, ClientQuestionKeywords))
        {
            return "Tirar duvidas";
        }

        return "Solicitar atendimento";
    }

    private static string ResolveProviderIntent(string normalizedText)
    {
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return "Cadastro como prestador";
        }

        if (ContainsAny(normalizedText, ProviderRegistrationKeywords))
        {
            return "Cadastro como prestador";
        }

        if (ContainsAny(normalizedText, ProviderOpportunityKeywords))
        {
            return "Receber oportunidades";
        }

        return "Parceria operacional";
    }

    private static string ExtractServiceCategory(string normalizedText)
    {
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return string.Empty;
        }

        foreach (var category in ServiceCategoryMap)
        {
            if (ContainsAny(normalizedText, category.Keywords))
            {
                return category.Category;
            }
        }

        return string.Empty;
    }

    private static string ExtractCityOrRegion(string originalText)
    {
        if (string.IsNullOrWhiteSpace(originalText))
        {
            return string.Empty;
        }

        foreach (Match match in LocationPatternRegex.Matches(originalText))
        {
            var candidate = CleanLocationCandidate(match.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static string ExtractPostalCode(string originalText)
    {
        if (string.IsNullOrWhiteSpace(originalText))
        {
            return string.Empty;
        }

        var match = PostalCodeRegex.Match(originalText);
        if (!match.Success)
        {
            return string.Empty;
        }

        var digits = new string(match.Value.Where(char.IsDigit).ToArray());
        return digits.Length == 8
            ? $"{digits[..5]}-{digits[5..]}"
            : string.Empty;
    }

    private static string BuildStatusNote(TelegramLeadQualification qualification)
    {
        var fragments = new List<string>();
        var boardIsProvider = string.Equals(qualification.BoardType, ProvidersBoardType, StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(qualification.City))
        {
            fragments.Add(boardIsProvider
                ? $"regiao {qualification.City}"
                : $"cidade {qualification.City}");
        }

        if (!string.IsNullOrWhiteSpace(qualification.ServiceCategory))
        {
            fragments.Add(boardIsProvider
                ? $"categoria tecnica {qualification.ServiceCategory}"
                : $"categoria {qualification.ServiceCategory}");
        }

        if (!string.IsNullOrWhiteSpace(qualification.Intent))
        {
            fragments.Add(boardIsProvider
                ? $"objetivo {qualification.Intent}"
                : $"intencao {qualification.Intent}");
        }

        if (fragments.Count == 0)
        {
            return boardIsProvider
                ? "Contato inicial de prestador recebido pelo bot Telegram."
                : "Contato inicial recebido pelo bot Telegram.";
        }

        return $"Lead Telegram qualificado: {string.Join("; ", fragments)}.";
    }

    private static string BuildQualificationPrompt(
        TelegramLeadQualification qualification,
        TelegramCapturedContact capturedContact,
        bool leadCreatedNow)
    {
        var boardIsProvider = string.Equals(qualification.BoardType, ProvidersBoardType, StringComparison.OrdinalIgnoreCase);
        var needsLocation = string.IsNullOrWhiteSpace(qualification.City);
        var needsCategory = string.IsNullOrWhiteSpace(qualification.ServiceCategory);
        var hasPhone = capturedContact.HasPhone;
        var hasEmail = capturedContact.HasEmail;

        if (!hasPhone)
        {
            return boardIsProvider
                ? "Recebi seu contato e ja registrei seu atendimento no funil de prestadores da ConsertaPraMim. Para agilizar, toque no botao abaixo e compartilhe seu telefone. Se puder, me diga tambem sua regiao de atendimento, categoria tecnica e objetivo principal."
                : "Recebi sua mensagem e ja registrei seu atendimento na ConsertaPraMim. Para agilizar, toque no botao abaixo e compartilhe seu telefone. Se puder, me diga tambem sua cidade, o tipo de servico e o que voce precisa resolver.";
        }

        if (needsLocation || needsCategory)
        {
            return boardIsProvider
                ? "Recebi seu telefone e atualizei seu atendimento. Agora me diga sua regiao de atendimento, categoria tecnica e objetivo principal para qualificar melhor o lead."
                : "Recebi seu telefone e atualizei seu atendimento. Agora me diga sua cidade, o tipo de servico e o que voce precisa resolver para qualificar melhor o lead.";
        }

        if (hasEmail)
        {
            return leadCreatedNow
                ? "Recebi seu contato, ja qualifiquei o lead e atualizei seu atendimento na ConsertaPraMim. Nosso time segue acompanhando por aqui."
                : "Recebi seu telefone e seu e-mail, e atualizei seu atendimento na ConsertaPraMim. Nosso time segue acompanhando por aqui.";
        }

        return boardIsProvider
            ? "Recebi seu telefone e ja qualifiquei seu cadastro inicial de prestador. Se quiser, voce tambem pode enviar seu e-mail por mensagem."
            : "Recebi seu telefone e ja qualifiquei seu atendimento inicial. Se quiser, voce tambem pode enviar seu e-mail por mensagem.";
    }

    private static string CleanLocationCandidate(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.Empty;
        }

        var cleaned = candidate.Trim().Trim('.', ',', ';', ':', '-', '/');
        cleaned = Regex.Replace(
            cleaned,
            @"\s+(?:e\s+)?(?:quero|preciso|gostaria|sou|tenho|estou|busco|procuro|para|pra|com|porque|pois)\b.*$",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

        var normalized = NormalizeMessage(cleaned) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized) || LocationNoiseTokens.Contains(normalized, StringComparer.Ordinal))
        {
            return string.Empty;
        }

        if (normalized.Length < 3)
        {
            return string.Empty;
        }

        var textInfo = new CultureInfo("pt-BR").TextInfo;
        return textInfo.ToTitleCase(cleaned.ToLowerInvariant());
    }

    private static bool ContainsAny(string source, IEnumerable<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            if (source.Contains(keyword, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static DateTime ResolveSentAtUtc(TelegramMessage message)
    {
        return message.DateUnix > 0
            ? DateTimeOffset.FromUnixTimeSeconds(message.DateUnix).UtcDateTime
            : DateTime.UtcNow;
    }

    private static Guid BuildDeterministicGuid(string scope, string value)
    {
        var raw = Encoding.UTF8.GetBytes($"{scope}:{value}");
        var hash = SHA256.HashData(raw);
        var bytes = hash[..16];
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private static string? NormalizeMessage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var decomposed = value.Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string TrimTo(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private static bool IsTrustedSharedContact(TelegramContact contact, TelegramMessage message)
    {
        if (!contact.UserId.HasValue)
        {
            return true;
        }

        if (message.From?.Id > 0)
        {
            return contact.UserId.Value == message.From.Id;
        }

        return contact.UserId.Value == message.Chat?.Id;
    }

    private static bool ShouldAcceptTextualPhone(string value)
    {
        var normalized = NormalizeMessage(value) ?? string.Empty;
        var digitsOnly = new string(value.Where(char.IsDigit).ToArray());
        var compact = new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray());
        var looksLikeDirectPhone = digitsOnly.Length is >= 10 and <= 15 && compact.Length <= 24;

        return looksLikeDirectPhone ||
               normalized.Contains("telefone", StringComparison.Ordinal) ||
               normalized.Contains("fone", StringComparison.Ordinal) ||
               normalized.Contains("contato", StringComparison.Ordinal) ||
               normalized.Contains("numero", StringComparison.Ordinal) ||
               normalized.Contains("whatsapp", StringComparison.Ordinal);
    }

    private static bool ShouldAcceptTextualEmail(string value, string email)
    {
        var trimmed = value.Trim();
        if (string.Equals(trimmed, email, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalized = NormalizeMessage(value) ?? string.Empty;
        return normalized.Contains("email", StringComparison.Ordinal) ||
               normalized.Contains("e-mail", StringComparison.Ordinal) ||
               normalized.Contains("meu email", StringComparison.Ordinal);
    }

    private static bool TryNormalizePhone(string? value, out string normalizedPhone)
    {
        normalizedPhone = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length is < 10 or > 15)
        {
            return false;
        }

        normalizedPhone = trimmed.StartsWith('+')
            ? $"+{digits}"
            : digits;

        return true;
    }

    private readonly record struct TelegramInboundBootstrapResult(
        bool Enabled,
        Guid ChatbotConversationId,
        string BoardType,
        bool LeadCreated,
        int LeadId,
        bool Succeeded,
        TelegramLeadQualification Qualification)
    {
        public static TelegramInboundBootstrapResult Disabled =>
            new(false, Guid.Empty, string.Empty, false, 0, false, TelegramLeadQualification.Empty);
    }

    private readonly record struct TelegramCapturedContact(
        string Phone,
        string Email,
        bool SharedNativeContact)
    {
        public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);
        public bool HasEmail => !string.IsNullOrWhiteSpace(Email);
    }

    private readonly record struct TelegramAutomaticResponse(
        string Text,
        TelegramMessageSendOptions? Options);

    private readonly record struct TelegramLeadQualification(
        string BoardType,
        string ServiceCategory,
        string City,
        string PostalCode,
        string Intent)
    {
        public static TelegramLeadQualification Empty =>
            new(ClientsBoardType, string.Empty, string.Empty, string.Empty, string.Empty);
    }
}
