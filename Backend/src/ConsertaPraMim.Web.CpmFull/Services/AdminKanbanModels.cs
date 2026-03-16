namespace AppMobileCPM.Services;

public static class AdminKanbanBoardTypes
{
    public const string Clients = "clientes";
    public const string Providers = "prestadores";

    public static bool IsValid(string? boardType) =>
        string.Equals(boardType, Clients, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(boardType, Providers, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? boardType)
    {
        if (string.Equals(boardType, Clients, StringComparison.OrdinalIgnoreCase))
        {
            return Clients;
        }

        if (string.Equals(boardType, Providers, StringComparison.OrdinalIgnoreCase))
        {
            return Providers;
        }

        throw new ArgumentException("Tipo de funil invalido.", nameof(boardType));
    }

    public static string GetTitle(string boardType) =>
        Normalize(boardType) switch
        {
            Clients => "Funil de Atendimento - Clientes",
            Providers => "Onboarding e Contato - Prestadores",
            _ => "Funil"
        };

    public static string GetSubtitle(string boardType) =>
        Normalize(boardType) switch
        {
            Clients => "Gerencie o ciclo de atendimento desde o primeiro contato ate a conclusao.",
            Providers => "Acompanhe o onboarding, validacao e ativacao de prestadores na plataforma.",
            _ => "Gerencie seu funil"
        };
}

public static class AdminKanbanJourneySourceChannels
{
    public const string Landing = "landing";
    public const string Telegram = "telegram";
    public const string ServiceRequest = "service_request";

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        Landing => Landing,
        Telegram => Telegram,
        ServiceRequest => ServiceRequest,
        _ => throw new ArgumentException("Canal de jornada invalido.", nameof(value))
    };

    public static string GetLabel(string? value) => Normalize(value) switch
    {
        Landing => "Landing / Site",
        Telegram => "Telegram",
        ServiceRequest => "Portal do Cliente",
        _ => "Canal desconhecido"
    };
}

public static class AdminKanbanJourneyStates
{
    public const string IntakeOpened = "intake_aberto";
    public const string AutomatedTriage = "triagem_automatica";
    public const string QualificationPending = "dados_pendentes";
    public const string QualificationConfirmationRequired = "confirmacao_necessaria";
    public const string QualificationValidated = "qualificacao_validada";
    public const string SlotSuggested = "janela_sugerida";
    public const string WaitingScheduleConfirmation = "aguardando_confirmacao_agenda";
    public const string AppointmentConfirmed = "agendamento_confirmado";
    public const string AppointmentCancelled = "agendamento_cancelado";
    public const string MatchingInProgress = "em_matching";
    public const string DispatchInProgress = "disparo_prestadores";
    public const string WaitingProviderAcceptance = "aguardando_aceite";
    public const string ProviderConnected = "prestador_conectado";
    public const string ServiceInProgress = "servico_em_andamento";
    public const string WaitingCompletionConfirmation = "aguardando_confirmacao_conclusao";
    public const string WaitingClientReview = "aguardando_avaliacao_cliente";
    public const string WaitingProviderReview = "aguardando_avaliacao_prestador";
    public const string Completed = "concluido";
    public const string NoMatch = "sem_match";
    public const string Cancelled = "cancelado";
    public const string OperationalException = "excecao_operacional";
    public const string ServiceRequestOpened = "pedido_aberto";

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        IntakeOpened => IntakeOpened,
        AutomatedTriage => AutomatedTriage,
        QualificationPending => QualificationPending,
        QualificationConfirmationRequired => QualificationConfirmationRequired,
        QualificationValidated => QualificationValidated,
        SlotSuggested => SlotSuggested,
        WaitingScheduleConfirmation => WaitingScheduleConfirmation,
        AppointmentConfirmed => AppointmentConfirmed,
        AppointmentCancelled => AppointmentCancelled,
        MatchingInProgress => MatchingInProgress,
        DispatchInProgress => DispatchInProgress,
        WaitingProviderAcceptance => WaitingProviderAcceptance,
        ProviderConnected => ProviderConnected,
        ServiceInProgress => ServiceInProgress,
        WaitingCompletionConfirmation => WaitingCompletionConfirmation,
        WaitingClientReview => WaitingClientReview,
        WaitingProviderReview => WaitingProviderReview,
        Completed => Completed,
        NoMatch => NoMatch,
        Cancelled => Cancelled,
        OperationalException => OperationalException,
        ServiceRequestOpened => ServiceRequestOpened,
        _ => IntakeOpened
    };

    public static string GetLabel(string? value) => Normalize(value) switch
    {
        IntakeOpened => "Intake aberto",
        AutomatedTriage => "Triagem automatica",
        QualificationPending => "Dados pendentes",
        QualificationConfirmationRequired => "Confirmacao necessaria",
        QualificationValidated => "Qualificacao validada",
        SlotSuggested => "Janela sugerida",
        WaitingScheduleConfirmation => "Aguardando confirmacao da agenda",
        AppointmentConfirmed => "Agendamento confirmado",
        AppointmentCancelled => "Agendamento cancelado",
        MatchingInProgress => "Em matching",
        DispatchInProgress => "Disparo para prestadores",
        WaitingProviderAcceptance => "Aguardando aceite",
        ProviderConnected => "Prestador conectado",
        ServiceInProgress => "Servico em andamento",
        WaitingCompletionConfirmation => "Aguardando confirmacao de conclusao",
        WaitingClientReview => "Aguardando avaliacao do cliente",
        WaitingProviderReview => "Aguardando avaliacao do prestador",
        Completed => "Concluido",
        NoMatch => "Sem match",
        Cancelled => "Cancelado",
        OperationalException => "Excecao operacional",
        ServiceRequestOpened => "Pedido aberto",
        _ => "Intake aberto"
    };

    public static int GetSortOrder(string? value) => Normalize(value) switch
    {
        IntakeOpened => 1,
        AutomatedTriage => 2,
        QualificationPending => 3,
        QualificationConfirmationRequired => 4,
        QualificationValidated => 5,
        SlotSuggested => 6,
        WaitingScheduleConfirmation => 7,
        AppointmentConfirmed => 8,
        AppointmentCancelled => 9,
        MatchingInProgress => 10,
        DispatchInProgress => 11,
        WaitingProviderAcceptance => 12,
        ProviderConnected => 13,
        ServiceInProgress => 14,
        WaitingCompletionConfirmation => 15,
        WaitingClientReview => 16,
        WaitingProviderReview => 17,
        Completed => 18,
        NoMatch => 19,
        Cancelled => 20,
        OperationalException => 21,
        ServiceRequestOpened => 22,
        _ => 1
    };
}

public static class AdminKanbanJourneyClientStageNames
{
    public const string NewLead = "Novo lead";
    public const string AutomatedTriage = "Triagem automatica";
    public const string PendingData = "Dados pendentes";
    public const string ValidatedAddressAndCategory = "Endereco e categoria validados";
    public const string SlotSuggested = "Janela sugerida";
    public const string WaitingScheduleConfirmation = "Aguardando confirmacao da agenda";
    public const string AppointmentConfirmed = "Agendamento confirmado";
    public const string MatchingInProgress = "Em matching";
    public const string DispatchInProgress = "Disparo para prestadores";
    public const string WaitingAcceptance = "Aguardando aceite";
    public const string ProviderConnected = "Prestador conectado";
    public const string ServiceInProgress = "Servico em andamento";
    public const string WaitingCompletionConfirmation = "Aguardando confirmacao de conclusao";
    public const string WaitingClientReview = "Aguardando avaliacao do cliente";
    public const string WaitingProviderReview = "Aguardando avaliacao do prestador";
    public const string Completed = "Concluido";
    public const string NoMatch = "Sem match";
    public const string Cancelled = "Cancelado";
    public const string OperationalException = "Excecao operacional";
}

public static class AdminKanbanJourneyAutomationOrigins
{
    public const string StateMachine = "state_machine";
    public const string Timer = "timer";

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        Timer => Timer,
        _ => StateMachine
    };

    public static string GetLabel(string? value) => Normalize(value) switch
    {
        Timer => "Timer operacional",
        _ => "Maquina de estados"
    };
}

public static class AdminKanbanJourneyTimerCodes
{
    public const string PendingData = "dados_pendentes";
    public const string PendingScheduleConfirmation = "agenda_pendente";
    public const string PendingAcceptance = "aceite_pendente";
    public const string PendingClientReview = "avaliacao_cliente_pendente";
    public const string PendingProviderReview = "avaliacao_prestador_pendente";

    public static string GetLabel(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        PendingData => "Dados pendentes",
        PendingScheduleConfirmation => "Confirmacao da agenda pendente",
        PendingAcceptance => "Aceite do prestador pendente",
        PendingClientReview => "Avaliacao do cliente pendente",
        PendingProviderReview => "Avaliacao do prestador pendente",
        _ => "-"
    };
}

public static class AdminKanbanJourneyQualificationStatuses
{
    public const string Pending = "dados_pendentes";
    public const string ConfirmationRequired = "confirmacao_necessaria";
    public const string Qualified = "qualificacao_validada";

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        Pending => Pending,
        ConfirmationRequired => ConfirmationRequired,
        Qualified => Qualified,
        _ => Pending
    };

    public static string GetLabel(string? value) => Normalize(value) switch
    {
        Pending => "Dados pendentes",
        ConfirmationRequired => "Confirmacao necessaria",
        Qualified => "Qualificacao validada",
        _ => "Dados pendentes"
    };
}

public static class AdminKanbanJourneyQualificationSources
{
    public const string Deterministic = "deterministico";
    public const string OpenAi = "openai";
    public const string Hybrid = "hibrido";

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        OpenAi => OpenAi,
        Hybrid => Hybrid,
        _ => Deterministic
    };

    public static string GetLabel(string? value) => Normalize(value) switch
    {
        OpenAi => "OpenAI",
        Hybrid => "Hibrido",
        _ => "Deterministico"
    };
}

public static class AdminKanbanJourneySchedulingStatuses
{
    public const string NotStarted = "nao_iniciado";
    public const string SlotSuggested = "janela_sugerida";
    public const string Confirmed = "confirmado";
    public const string Cancelled = "cancelado";
    public const string NoAvailability = "sem_disponibilidade";

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        SlotSuggested => SlotSuggested,
        Confirmed => Confirmed,
        Cancelled => Cancelled,
        NoAvailability => NoAvailability,
        _ => NotStarted
    };

    public static string GetLabel(string? value) => Normalize(value) switch
    {
        SlotSuggested => "Janela sugerida",
        Confirmed => "Confirmado",
        Cancelled => "Cancelado",
        NoAvailability => "Sem disponibilidade",
        _ => "Nao iniciado"
    };
}

public sealed class AdminKanbanBoardData
{
    public required string BoardType { get; init; }
    public required IReadOnlyList<AdminKanbanStageRecord> Stages { get; init; }
}

public sealed class AdminKanbanStageRecord
{
    public int Id { get; init; }
    public required string BoardType { get; init; }
    public required string Name { get; init; }
    public string Color { get; init; } = "#0d6efd";
    public int SortOrder { get; init; }
    public required IReadOnlyList<AdminKanbanLeadCardRecord> Leads { get; init; }
}

public sealed class AdminKanbanLeadCardRecord
{
    public int Id { get; init; }
    public int StageId { get; init; }
    public required string BoardType { get; init; }
    public required string Name { get; init; }
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ServiceCategory { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Priority { get; init; } = "normal";
    public string StatusNote { get; init; } = string.Empty;
    public string ChatwootSyncStatus { get; init; } = string.Empty;
    public DateTime StageEnteredAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? LastContactAt { get; init; }
}

public sealed class AdminKanbanLeadDetailsRecord
{
    public int Id { get; init; }
    public int StageId { get; init; }
    public required string StageName { get; init; }
    public required string BoardType { get; init; }
    public required string Name { get; init; }
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ServiceCategory { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Priority { get; init; } = "normal";
    public string StatusNote { get; init; } = string.Empty;
    public string InternalNotes { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? LastContactAt { get; init; }
    public AdminKanbanLeadJourneyRecord Journey { get; init; } = new();
    public AdminKanbanLeadTelegramLinkRecord Telegram { get; init; } = new();
    public AdminKanbanLeadChatwootSyncRecord Chatwoot { get; init; } = new();
    public required IReadOnlyList<AdminKanbanLeadHistoryRecord> History { get; init; }
}

public sealed class AdminKanbanLeadTelegramLinkRecord
{
    public Guid? ChatbotConversationId { get; init; }
    public string ChannelConversationId { get; init; } = string.Empty;
    public long? TelegramChatId { get; init; }
    public Guid? ClientId { get; init; }
    public string ClientPhone { get; init; } = string.Empty;
    public string ClientEmail { get; init; } = string.Empty;
    public Guid? ServiceRequestId { get; init; }
    public DateTime? HumanHandoffStartedAt { get; init; }
    public string HumanHandoffStatus { get; init; } = string.Empty;
    public string HumanHandoffReason { get; init; } = string.Empty;
    public DateTime? HumanHandoffUpdatedAt { get; init; }
    public DateTime? LastTelegramMessageSyncedAt { get; init; }
    public DateTime? LastChatwootMessageSyncedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AdminKanbanLeadChatwootSyncRecord
{
    public long? ContactId { get; init; }
    public long? ConversationId { get; init; }
    public long? InboxId { get; init; }
    public string SyncStatus { get; init; } = string.Empty;
    public DateTime? LastSyncAt { get; init; }
    public string LastError { get; init; } = string.Empty;
}

public sealed class AdminKanbanLeadHistoryRecord
{
    public int Id { get; init; }
    public required string EventType { get; init; }
    public int? FromStageId { get; init; }
    public string FromStageName { get; init; } = string.Empty;
    public int? ToStageId { get; init; }
    public string ToStageName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class AdminKanbanLeadUpsertRequest
{
    public required string BoardType { get; init; }
    public int StageId { get; init; }
    public required string Name { get; init; }
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ServiceCategory { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Priority { get; init; } = "normal";
    public string StatusNote { get; init; } = string.Empty;
    public string InternalNotes { get; init; } = string.Empty;
    public DateTime? LastContactAt { get; init; }
}

public sealed class AdminKanbanTelegramLeadUpsertRequest
{
    public required string BoardType { get; init; }
    public required Guid ChatbotConversationId { get; init; }
    public required string ChannelConversationId { get; init; }
    public long TelegramChatId { get; init; }
    public Guid ClientId { get; init; }
    public required string ClientName { get; init; }
    public string ClientPhone { get; init; } = string.Empty;
    public string ClientEmail { get; init; } = string.Empty;
    public Guid? ServiceRequestId { get; init; }
    public string ServiceCategory { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string StatusNote { get; init; } = string.Empty;
    public string InternalNotes { get; init; } = string.Empty;
    public DateTime? LastContactAt { get; init; }
}

public sealed class AdminKanbanTelegramLinkTouchRequest
{
    public DateTime? HumanHandoffStartedAt { get; init; }
    public string HumanHandoffStatus { get; init; } = string.Empty;
    public string HumanHandoffReason { get; init; } = string.Empty;
    public DateTime? HumanHandoffUpdatedAt { get; init; }
    public DateTime? LastTelegramMessageSyncedAt { get; init; }
    public DateTime? LastChatwootMessageSyncedAt { get; init; }
}

public sealed record class AdminKanbanTelegramLeadUpsertResult
{
    public int LeadId { get; init; }
    public bool Created { get; init; }
    public int StageId { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public Guid ChatbotConversationId { get; init; }
}

public sealed class AdminKanbanTelegramDeliveryQueueEnqueueRequest
{
    public int LeadId { get; init; }
    public string Direction { get; init; } = string.Empty;
    public string DeliveryKey { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public long? ChatwootConversationId { get; init; }
    public long? TelegramChatId { get; init; }
    public DateTime NextAttemptAt { get; init; }
    public int MaxAttempts { get; init; }
    public string? LastError { get; init; }
}

public sealed class AdminKanbanTelegramDeliveryQueueFinalizeRequest
{
    public int QueueItemId { get; init; }
    public string FinalStatus { get; init; } = string.Empty;
    public DateTime FinalizedAt { get; init; }
    public DateTime? NextAttemptAt { get; init; }
    public string? LastError { get; init; }
    public bool ClearLastError { get; init; }
    public string WorkerInstance { get; init; } = string.Empty;
}

public sealed record class AdminKanbanTelegramDeliveryQueueItemRecord
{
    public int Id { get; init; }
    public int LeadId { get; init; }
    public string Direction { get; init; } = string.Empty;
    public string DeliveryKey { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public long? ChatwootConversationId { get; init; }
    public long? TelegramChatId { get; init; }
    public string Status { get; init; } = string.Empty;
    public int AttemptCount { get; init; }
    public int MaxAttempts { get; init; }
    public DateTime NextAttemptAt { get; init; }
    public DateTime? LastAttemptAt { get; init; }
    public string LastError { get; init; } = string.Empty;
    public string WorkerInstance { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
    public DateTime? DeadLetterAt { get; init; }
    public bool IsDuplicate { get; init; }
}

public sealed record class AdminKanbanTelegramSyncIssueRecord
{
    public int QueueItemId { get; init; }
    public int LeadId { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public string StageName { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int AttemptCount { get; init; }
    public int MaxAttempts { get; init; }
    public DateTime? LastAttemptAt { get; init; }
    public string LastError { get; init; } = string.Empty;
    public long? ChatwootConversationId { get; init; }
    public long? TelegramChatId { get; init; }
}

public sealed record class AdminKanbanTelegramQueueDiagnosticRecord
{
    public int QueueItemId { get; init; }
    public int LeadId { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public string StageName { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int AttemptCount { get; init; }
    public int MaxAttempts { get; init; }
    public DateTime NextAttemptAt { get; init; }
    public DateTime? LastAttemptAt { get; init; }
    public string LastError { get; init; } = string.Empty;
    public long? ChatwootConversationId { get; init; }
    public long? TelegramChatId { get; init; }
}

public sealed record class AdminKanbanTelegramDiagnosticsSnapshot
{
    public string ScopeBoardType { get; init; } = string.Empty;
    public int TotalTelegramLeads { get; init; }
    public int LeadsWithInboundMirror { get; init; }
    public int LeadsWithOutboundMirror { get; init; }
    public int HumanHandoffCount { get; init; }
    public int ActiveQueueCount { get; init; }
    public int DeadLetterCount { get; init; }
    public IReadOnlyList<AdminKanbanTelegramSyncIssueRecord> RecentIssues { get; init; } = [];
    public IReadOnlyList<AdminKanbanTelegramQueueDiagnosticRecord> RecentQueueItems { get; init; } = [];
}

public sealed class AdminKanbanTelegramBusinessDashboardFilter
{
    public string? BoardType { get; init; }
    public DateTime CreatedFromUtc { get; init; }
    public DateTime CreatedToUtcExclusive { get; init; }
    public int BreakdownLimit { get; init; } = 8;
}

public sealed record class AdminKanbanTelegramBusinessBoardBreakdownRecord
{
    public string BoardType { get; init; } = string.Empty;
    public int TotalLeads { get; init; }
    public int QualifiedLeadCount { get; init; }
    public int LeadsWithContactInfo { get; init; }
    public int LeadsWithChatwootConversation { get; init; }
    public int LeadsWithHumanHandoff { get; init; }
    public decimal? AverageMinutesToChatwoot { get; init; }
    public decimal? AverageMinutesToHandoff { get; init; }
}

public sealed record class AdminKanbanTelegramBusinessDailyVolumeRecord
{
    public DateTime ReferenceDateLocal { get; init; }
    public int ClientsLeads { get; init; }
    public int ProvidersLeads { get; init; }
    public int TotalLeads { get; init; }
}

public sealed record class AdminKanbanTelegramBusinessCategoryRecord
{
    public string ServiceCategory { get; init; } = string.Empty;
    public int TotalLeads { get; init; }
    public int LeadsWithChatwootConversation { get; init; }
    public int LeadsWithHumanHandoff { get; init; }
}

public sealed record class AdminKanbanTelegramBusinessCityRecord
{
    public string City { get; init; } = string.Empty;
    public int TotalLeads { get; init; }
    public int LeadsWithChatwootConversation { get; init; }
    public int LeadsWithHumanHandoff { get; init; }
}

public sealed record class AdminKanbanTelegramBusinessStagePressureRecord
{
    public string BoardType { get; init; } = string.Empty;
    public string StageName { get; init; } = string.Empty;
    public int TotalLeads { get; init; }
    public int LeadsWithoutContactInfo { get; init; }
    public int LeadsWithoutChatwootConversation { get; init; }
    public int LeadsWithoutRecentContact { get; init; }
    public decimal? AverageLeadAgeHours { get; init; }
}

public sealed record class AdminKanbanTelegramBusinessHandoffReasonRecord
{
    public string Reason { get; init; } = string.Empty;
    public int TotalLeads { get; init; }
}

public sealed record class AdminKanbanTelegramBusinessDashboardSnapshot
{
    public string ScopeBoardType { get; init; } = string.Empty;
    public DateTime CreatedFromUtc { get; init; }
    public DateTime CreatedToUtcExclusive { get; init; }
    public int TotalTelegramLeads { get; init; }
    public int ClientsLeads { get; init; }
    public int ProvidersLeads { get; init; }
    public int LeadsWithPhone { get; init; }
    public int LeadsWithEmail { get; init; }
    public int LeadsWithContactInfo { get; init; }
    public int LeadsWithQualifiedCategory { get; init; }
    public int LeadsWithQualifiedCity { get; init; }
    public int LeadsWithChatwootConversation { get; init; }
    public int LeadsWithHumanHandoff { get; init; }
    public int MedianMinutesToChatwoot { get; init; }
    public int MedianMinutesToHandoff { get; init; }
    public IReadOnlyList<AdminKanbanTelegramBusinessBoardBreakdownRecord> BoardBreakdown { get; init; } = [];
    public IReadOnlyList<AdminKanbanTelegramBusinessDailyVolumeRecord> DailyVolumes { get; init; } = [];
    public IReadOnlyList<AdminKanbanTelegramBusinessCategoryRecord> TopCategories { get; init; } = [];
    public IReadOnlyList<AdminKanbanTelegramBusinessCityRecord> TopCities { get; init; } = [];
    public IReadOnlyList<AdminKanbanTelegramBusinessStagePressureRecord> StagePressures { get; init; } = [];
    public IReadOnlyList<AdminKanbanTelegramBusinessHandoffReasonRecord> HandoffReasons { get; init; } = [];
}

public sealed class AdminKanbanJourneyIntakeRequest
{
    public required string BoardType { get; init; }
    public required string SourceChannel { get; init; }
    public string SourceOrigin { get; init; } = string.Empty;
    public required string Name { get; init; }
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ServiceCategory { get; init; } = string.Empty;
    public string ProblemDescription { get; init; } = string.Empty;
    public string Street { get; init; } = string.Empty;
    public string Neighborhood { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string StatusNote { get; init; } = string.Empty;
    public string InternalNotes { get; init; } = string.Empty;
    public Guid? LandingLeadId { get; init; }
    public Guid? ServiceRequestId { get; init; }
    public Guid? ClientId { get; init; }
    public string VisitorId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public Guid? ChatbotConversationId { get; init; }
    public string ChannelConversationId { get; init; } = string.Empty;
    public long? TelegramChatId { get; init; }
    public DateTime? RequestedAtUtc { get; init; }
    public DateTime? LastContactAtUtc { get; init; }
    public AdminKanbanJourneyQualificationRecord Qualification { get; init; } = new();
    public AdminKanbanJourneySchedulingRecord Scheduling { get; init; } = new();
}

public sealed record class AdminKanbanJourneyUpsertResult
{
    public int LeadId { get; init; }
    public int JourneyId { get; init; }
    public Guid JourneyPublicId { get; init; }
    public bool CreatedLead { get; init; }
    public bool CreatedJourney { get; init; }
    public int StageId { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public string CurrentState { get; init; } = string.Empty;
}

public sealed class AdminKanbanLeadJourneyRecord
{
    public int JourneyId { get; init; }
    public Guid JourneyPublicId { get; init; }
    public int LeadId { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public string JourneyKey { get; init; } = string.Empty;
    public string SourceChannel { get; init; } = string.Empty;
    public string SourceOrigin { get; init; } = string.Empty;
    public string CurrentState { get; init; } = string.Empty;
    public Guid? LandingLeadId { get; init; }
    public Guid? ServiceRequestId { get; init; }
    public Guid? ClientId { get; init; }
    public string VisitorId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public Guid? ChatbotConversationId { get; init; }
    public string ChannelConversationId { get; init; } = string.Empty;
    public long? TelegramChatId { get; init; }
    public string PrimaryPhone { get; init; } = string.Empty;
    public string PrimaryEmail { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? LastIntakeAt { get; init; }
    public AdminKanbanJourneyQualificationRecord Qualification { get; init; } = new();
    public AdminKanbanJourneySchedulingRecord Scheduling { get; init; } = new();
    public AdminKanbanJourneyStageAutomationRecord StageAutomation { get; init; } = new();
}

public sealed class AdminKanbanJourneyQualificationRecord
{
    public string Status { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public decimal ConfidenceScore { get; init; }
    public bool HasRequiredData { get; init; }
    public bool NeedsConfirmation { get; init; }
    public string NormalizedServiceCategoryId { get; init; } = string.Empty;
    public string NormalizedServiceCategoryName { get; init; } = string.Empty;
    public string ProblemContext { get; init; } = string.Empty;
    public string Street { get; init; } = string.Empty;
    public string Neighborhood { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string ConfirmationPrompt { get; init; } = string.Empty;
    public DateTime? QualifiedAtUtc { get; init; }
    public IReadOnlyList<string> RequiredFields { get; init; } = [];
    public IReadOnlyList<string> MissingRequiredFields { get; init; } = [];
    public IReadOnlyList<string> OptionalFields { get; init; } = [];
}

public sealed class AdminKanbanJourneySchedulingRecord
{
    public string Status { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string GoogleCalendarEventId { get; init; } = string.Empty;
    public string GoogleCalendarEventLink { get; init; } = string.Empty;
    public DateTime? SuggestedAtUtc { get; init; }
    public DateTime? ConfirmedAtUtc { get; init; }
    public DateTime? CancelledAtUtc { get; init; }
    public DateTime? ScheduledStartAtUtc { get; init; }
    public DateTime? ScheduledEndAtUtc { get; init; }
    public IReadOnlyList<AdminKanbanJourneySuggestedSlotRecord> SuggestedSlots { get; init; } = [];
}

public sealed class AdminKanbanJourneyStageAutomationRecord
{
    public string LastReason { get; init; } = string.Empty;
    public string LastOrigin { get; init; } = string.Empty;
    public DateTime? LastTransitionAtUtc { get; init; }
    public string ActiveTimerCode { get; init; } = string.Empty;
    public DateTime? ActiveTimerDueAtUtc { get; init; }
}

public sealed class AdminKanbanJourneySuggestedSlotRecord
{
    public int OptionNumber { get; init; }
    public DateTime StartsAtUtc { get; init; }
    public DateTime EndsAtUtc { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class AdminKanbanJourneySchedulingUpdateRequest
{
    public string Status { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string GoogleCalendarEventId { get; init; } = string.Empty;
    public string GoogleCalendarEventLink { get; init; } = string.Empty;
    public DateTime? SuggestedAtUtc { get; init; }
    public DateTime? ConfirmedAtUtc { get; init; }
    public DateTime? CancelledAtUtc { get; init; }
    public DateTime? ScheduledStartAtUtc { get; init; }
    public DateTime? ScheduledEndAtUtc { get; init; }
    public required string CurrentState { get; init; }
    public string HistoryEventType { get; init; } = string.Empty;
    public string HistoryDescription { get; init; } = string.Empty;
    public string SourceChannel { get; init; } = string.Empty;
    public string MetadataJson { get; init; } = string.Empty;
    public IReadOnlyList<AdminKanbanJourneySuggestedSlotRecord> SuggestedSlots { get; init; } = [];
}

public sealed record class AdminKanbanJourneySchedulingUpdateResult
{
    public int LeadId { get; init; }
    public int JourneyId { get; init; }
    public string CurrentState { get; init; } = string.Empty;
    public AdminKanbanJourneySchedulingRecord Scheduling { get; init; } = new();
}

public sealed record class AdminKanbanJourneyStageAutomationCandidateRecord
{
    public int LeadId { get; init; }
    public int JourneyId { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public int StageId { get; init; }
    public string StageName { get; init; } = string.Empty;
    public string CurrentState { get; init; } = string.Empty;
    public string QualificationStatus { get; init; } = string.Empty;
    public string SchedulingStatus { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? LastIntakeAtUtc { get; init; }
    public DateTime? CurrentStateEnteredAtUtc { get; init; }
    public DateTime? SchedulingSuggestedAtUtc { get; init; }
    public string ActiveTimerCode { get; init; } = string.Empty;
    public DateTime? ActiveTimerDueAtUtc { get; init; }
    public string LastAutomationReason { get; init; } = string.Empty;
    public string LastAutomationOrigin { get; init; } = string.Empty;
}

public sealed class AdminKanbanJourneyStageAutomationUpdateRequest
{
    public int LeadId { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public string TargetStageName { get; init; } = string.Empty;
    public string TargetCurrentState { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string HistoryEventType { get; init; } = string.Empty;
    public string HistoryDescription { get; init; } = string.Empty;
    public string MetadataJson { get; init; } = string.Empty;
    public string ActiveTimerCode { get; init; } = string.Empty;
    public DateTime? ActiveTimerDueAtUtc { get; init; }
}

public sealed record class AdminKanbanJourneyStageAutomationUpdateResult
{
    public int LeadId { get; init; }
    public int JourneyId { get; init; }
    public int FromStageId { get; init; }
    public string FromStageName { get; init; } = string.Empty;
    public int ToStageId { get; init; }
    public string ToStageName { get; init; } = string.Empty;
    public string CurrentState { get; init; } = string.Empty;
    public bool StageChanged { get; init; }
}

public sealed class AdminKanbanBoardOrderUpdateRequest
{
    public required string BoardType { get; init; }
    public int? ChangedLeadId { get; init; }
    public int? FromStageId { get; init; }
    public int? ToStageId { get; init; }
    public required IReadOnlyList<AdminKanbanStageOrderUpdateItem> Stages { get; init; }
}

public sealed class AdminKanbanStageOrderUpdateItem
{
    public int StageId { get; init; }
    public required IReadOnlyList<int> LeadIds { get; init; }
}

public sealed class AdminKanbanLeadChatwootSyncUpdateRequest
{
    public long? ChatwootContactId { get; init; }
    public long? ChatwootConversationId { get; init; }
    public long? ChatwootInboxId { get; init; }
    public string? ChatwootSyncStatus { get; init; }
    public DateTime? ChatwootLastSyncAt { get; init; }
    public string? ChatwootLastError { get; init; }
    public bool ClearChatwootLastError { get; init; }
}

public sealed class AdminKanbanLeadWebhookUpdateRequest
{
    public DateTime? LastContactAt { get; init; }
    public string HistoryEventType { get; init; } = string.Empty;
    public string HistoryDescription { get; init; } = string.Empty;
}

public sealed class AdminKanbanChatwootWebhookEventUpsertRequest
{
    public string? ProviderEventId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public long? ConversationId { get; init; }
    public string PayloadJson { get; init; } = string.Empty;
    public string Signature { get; init; } = string.Empty;
    public DateTime ReceivedAt { get; init; }
}

public sealed record class AdminKanbanChatwootWebhookEventRecord
{
    public int Id { get; init; }
    public string ProviderEventId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public long? ConversationId { get; init; }
    public string ProcessStatus { get; init; } = string.Empty;
    public DateTime ReceivedAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public bool IsDuplicate { get; init; }
}

public sealed class AdminKanbanChatwootSyncQueueEnqueueRequest
{
    public int LeadId { get; init; }
    public string OperationType { get; init; } = string.Empty;
    public DateTime NextAttemptAt { get; init; }
    public int MaxAttempts { get; init; }
    public string? LastError { get; init; }
}

public sealed class AdminKanbanChatwootSyncQueueFinalizeRequest
{
    public int QueueItemId { get; init; }
    public string FinalStatus { get; init; } = string.Empty;
    public DateTime FinalizedAt { get; init; }
    public DateTime? NextAttemptAt { get; init; }
    public string? LastError { get; init; }
    public bool ClearLastError { get; init; }
    public string WorkerInstance { get; init; } = string.Empty;
}

public sealed record class AdminKanbanChatwootSyncQueueItemRecord
{
    public int Id { get; init; }
    public int LeadId { get; init; }
    public string OperationType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int AttemptCount { get; init; }
    public int MaxAttempts { get; init; }
    public DateTime NextAttemptAt { get; init; }
    public DateTime? LastAttemptAt { get; init; }
    public string LastError { get; init; } = string.Empty;
    public string WorkerInstance { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
    public DateTime? DeadLetterAt { get; init; }
}

public sealed record class AdminKanbanChatwootBackfillCandidateRecord
{
    public int LeadId { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public string StageName { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public Guid? TelegramChatbotConversationId { get; init; }
    public string TelegramChannelConversationId { get; init; } = string.Empty;
    public long? TelegramChatId { get; init; }
    public long? ChatwootContactId { get; init; }
    public long? ChatwootInboxId { get; init; }
}

public sealed record class AdminKanbanChatwootBackfillCheckpointRecord
{
    public string ScopeKey { get; init; } = string.Empty;
    public int? LastProcessedLeadId { get; init; }
    public DateTime? LastRunStartedAt { get; init; }
    public DateTime? LastRunCompletedAt { get; init; }
    public string LastRunStatus { get; init; } = string.Empty;
    public string LastSummaryJson { get; init; } = string.Empty;
    public DateTime UpdatedAt { get; init; }
}

public sealed record class AdminKanbanChatwootSyncIssueRecord
{
    public int LeadId { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public string StageName { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string SyncStatus { get; init; } = string.Empty;
    public DateTime? LastSyncAt { get; init; }
    public string LastError { get; init; } = string.Empty;
    public long? ContactId { get; init; }
    public long? ConversationId { get; init; }
    public long? InboxId { get; init; }
}

public sealed record class AdminKanbanChatwootQueueDiagnosticRecord
{
    public int QueueItemId { get; init; }
    public int LeadId { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public string StageName { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string OperationType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int AttemptCount { get; init; }
    public int MaxAttempts { get; init; }
    public DateTime NextAttemptAt { get; init; }
    public DateTime? LastAttemptAt { get; init; }
    public string LastError { get; init; } = string.Empty;
    public long? ConversationId { get; init; }
}

public sealed record class AdminKanbanChatwootDiagnosticsSnapshot
{
    public string ScopeBoardType { get; init; } = string.Empty;
    public int TotalLeads { get; init; }
    public int SyncedCount { get; init; }
    public int PendingCount { get; init; }
    public int FailedCount { get; init; }
    public int ActiveQueueCount { get; init; }
    public int DeadLetterCount { get; init; }
    public IReadOnlyList<AdminKanbanChatwootSyncIssueRecord> RecentIssues { get; init; } = [];
    public IReadOnlyList<AdminKanbanChatwootQueueDiagnosticRecord> RecentQueueItems { get; init; } = [];
}

public sealed class AdminKanbanChatwootBackfillCheckpointUpsertRequest
{
    public string ScopeKey { get; init; } = string.Empty;
    public int? LastProcessedLeadId { get; init; }
    public DateTime? LastRunStartedAt { get; init; }
    public DateTime? LastRunCompletedAt { get; init; }
    public string LastRunStatus { get; init; } = string.Empty;
    public string LastSummaryJson { get; init; } = string.Empty;
}
