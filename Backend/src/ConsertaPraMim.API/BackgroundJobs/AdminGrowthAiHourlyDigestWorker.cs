using System.Net.Mail;
using System.Text;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.API.BackgroundJobs;

public class AdminGrowthAiHourlyDigestWorker : BackgroundService
{
    private const string DefaultTimeZoneId = "America/Sao_Paulo";
    private const string DefaultPrimaryRecipient = "devcraftstudio@outlook.com";
    private const string DefaultCcRecipient = "leonardomendes201704@gmail.com";
    private const string DefaultMonitoringRange = "24h";
    private const string DefaultSubjectPrefix = "[ConsertaPraMim] Relatorio horario IA";
    private const string SystemActorEmail = "system.hourly-digest@consertapramim.local";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdminGrowthAiHourlyDigestWorker> _logger;
    private readonly bool _enabled;
    private readonly string _timeZoneId;
    private readonly string _monitoringRange;
    private readonly string _subjectPrefix;
    private readonly string _primaryRecipient;
    private readonly IReadOnlyList<string> _ccRecipients;
    private readonly int _recentEventsTake;

    public AdminGrowthAiHourlyDigestWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AdminGrowthAiHourlyDigestWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _enabled = ParseBool(configuration["AdminGrowthAi:HourlyDigest:Enabled"], defaultValue: true);
        _timeZoneId = NormalizeText(configuration["AdminGrowthAi:HourlyDigest:TimeZoneId"], 120) ?? DefaultTimeZoneId;
        _monitoringRange = NormalizeText(configuration["AdminGrowthAi:HourlyDigest:MonitoringRange"], 20) ?? DefaultMonitoringRange;
        _subjectPrefix = NormalizeText(configuration["AdminGrowthAi:HourlyDigest:SubjectPrefix"], 120) ?? DefaultSubjectPrefix;
        _primaryRecipient = NormalizeEmail(configuration["AdminGrowthAi:HourlyDigest:PrimaryRecipient"]) ?? DefaultPrimaryRecipient;
        _ccRecipients = ParseRecipientList(
            configuration["AdminGrowthAi:HourlyDigest:CcRecipients"],
            [DefaultCcRecipient]);
        _recentEventsTake = Math.Clamp(ParseInt(configuration["AdminGrowthAi:HourlyDigest:RecentEventsTake"], 20), 5, 100);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("AdminGrowthAiHourlyDigestWorker disabled by configuration.");
            return;
        }

        var timeZone = ResolveTimeZone(_timeZoneId);
        _logger.LogInformation(
            "AdminGrowthAiHourlyDigestWorker started. TimeZone={TimeZoneId} PrimaryRecipient={PrimaryRecipient} CcCount={CcCount}",
            timeZone.Id,
            _primaryRecipient,
            _ccRecipients.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nowUtc = DateTime.UtcNow;
            var nextRunUtc = CalculateNextTopOfHourUtc(nowUtc, timeZone);
            var waitTime = nextRunUtc - nowUtc;
            if (waitTime > TimeSpan.Zero)
            {
                _logger.LogDebug(
                    "AdminGrowthAiHourlyDigestWorker waiting {Delay} until next full hour ({NextRunUtc:o}).",
                    waitTime,
                    nextRunUtc);
                await Task.Delay(waitTime, stoppingToken);
            }

            try
            {
                await RunOnceAsync(timeZone, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected failure in AdminGrowthAiHourlyDigestWorker.");
            }
        }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var timeZone = ResolveTimeZone(_timeZoneId);
        await RunOnceAsync(timeZone, cancellationToken);
    }

    private async Task RunOnceAsync(TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dashboardService = scope.ServiceProvider.GetRequiredService<IAdminDashboardService>();
        var monitoringService = scope.ServiceProvider.GetRequiredService<IAdminMonitoringService>();
        var growthAiService = scope.ServiceProvider.GetRequiredService<IAdminGrowthAiService>();
        var growthAiStore = scope.ServiceProvider.GetRequiredService<IAdminGrowthAiStore>();
        var growthAiGateway = scope.ServiceProvider.GetRequiredService<IAdminGrowthAiGateway>();
        var mailboxStore = scope.ServiceProvider.GetRequiredService<IAdminMailboxStore>();
        var mailboxGateway = scope.ServiceProvider.GetRequiredService<IAdminMailboxGateway>();

        var nowUtc = DateTime.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
        var dayStartLocal = new DateTime(
            nowLocal.Year,
            nowLocal.Month,
            nowLocal.Day,
            0,
            0,
            0,
            DateTimeKind.Unspecified);
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartLocal, timeZone);

        var dashboard = await dashboardService.GetDashboardAsync(
            new AdminDashboardQueryDto(
                FromUtc: dayStartUtc,
                ToUtc: nowUtc,
                EventType: null,
                OperationalStatus: null,
                SearchTerm: null,
                Page: 1,
                PageSize: _recentEventsTake));

        var monitoring = await monitoringService.GetOverviewAsync(
            new AdminMonitoringOverviewQueryDto(
                Range: _monitoringRange,
                Endpoint: null,
                StatusCode: null,
                UserId: null,
                TenantId: null,
                Severity: null),
            cancellationToken);

        var dailyAnalysisResult = await growthAiService.AnalyzeAsync(
            new AdminGrowthAiAnalyzeRequestDto(
                FromUtc: dayStartUtc,
                ToUtc: nowUtc,
                Category: null,
                City: null,
                ProposalSlaMinutes: 30,
                AcceptanceSlaHours: 24,
                LiquidityTake: 10),
            actorUserId: Guid.Empty,
            actorEmail: SystemActorEmail,
            cancellationToken: cancellationToken);

        var growthSnapshot = await growthAiStore.LoadAsync(cancellationToken);
        var latestStoredAnalysis = (growthSnapshot.Analyses ?? Array.Empty<AdminGrowthAiAnalysisDto>())
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();
        var effectiveAnalysis = dailyAnalysisResult.Analysis ?? latestStoredAnalysis;

        var payload = new HourlyDigestPayload(
            GeneratedAtUtc: nowUtc,
            WindowFromUtc: dayStartUtc,
            WindowToUtc: nowUtc,
            TimeZoneId: timeZone.Id,
            Dashboard: dashboard,
            Monitoring: monitoring,
            DailyAnalysis: dailyAnalysisResult,
            LatestStoredAnalysis: effectiveAnalysis);

        var htmlReport = await BuildHtmlReportAsync(
            payload,
            growthSnapshot.Settings,
            growthAiGateway,
            cancellationToken);

        await SendReportEmailAsync(
            payload,
            htmlReport,
            mailboxStore,
            mailboxGateway,
            cancellationToken);

        _logger.LogInformation(
            "AdminGrowthAi hourly digest delivered. GeneratedAtUtc={GeneratedAtUtc:o} PrimaryRecipient={PrimaryRecipient} CcCount={CcCount} DashboardEvents={DashboardEvents} MonitoringRequests={MonitoringRequests}",
            nowUtc,
            _primaryRecipient,
            _ccRecipients.Count,
            dashboard.RecentEvents.Count,
            monitoring.TotalRequests);
    }

    private async Task<string> BuildHtmlReportAsync(
        HourlyDigestPayload payload,
        AdminGrowthAiStoreSettings? growthSettings,
        IAdminGrowthAiGateway growthAiGateway,
        CancellationToken cancellationToken)
    {
        if (growthSettings == null ||
            !growthSettings.Enabled ||
            string.IsNullOrWhiteSpace(growthSettings.ApiKey))
        {
            return BuildFallbackHtml(
                payload,
                "OpenAI nao configurada/habilitada no modulo AdminGrowthAi. Foi enviado relatorio de fallback.");
        }

        var compactPayload = new
        {
            payload.GeneratedAtUtc,
            payload.WindowFromUtc,
            payload.WindowToUtc,
            payload.TimeZoneId,
            Dashboard = new
            {
                payload.Dashboard.TotalUsers,
                payload.Dashboard.ActiveUsers,
                payload.Dashboard.TotalProviders,
                payload.Dashboard.TotalClients,
                payload.Dashboard.TotalRequests,
                payload.Dashboard.ActiveRequests,
                payload.Dashboard.RequestsInPeriod,
                payload.Dashboard.ProposalsInPeriod,
                payload.Dashboard.AcceptedProposalsInPeriod,
                payload.Dashboard.MonthlySubscriptionRevenue,
                payload.Dashboard.RepurchaseRatePercent,
                payload.Dashboard.OperationalNpsScore,
                payload.Dashboard.OperationalQualityScore,
                RecentEvents = payload.Dashboard.RecentEvents
                    .Take(20)
                    .Select(item => new
                    {
                        item.Type,
                        item.CreatedAt,
                        item.Title,
                        item.Description
                    })
                    .ToArray()
            },
            Monitoring = new
            {
                payload.Monitoring.TotalRequests,
                payload.Monitoring.ErrorRatePercent,
                payload.Monitoring.P95LatencyMs,
                payload.Monitoring.RequestsPerMinute,
                payload.Monitoring.TopEndpoint,
                payload.Monitoring.ApiHealthStatus,
                payload.Monitoring.DatabaseHealthStatus,
                payload.Monitoring.ClientPortalHealthStatus,
                payload.Monitoring.ProviderPortalHealthStatus,
                TopErrors = payload.Monitoring.TopErrors
                    .Take(8)
                    .Select(item => new
                    {
                        item.ErrorKey,
                        item.ErrorType,
                        item.Message,
                        item.Count,
                        item.EndpointTemplate,
                        item.StatusCode
                    })
                    .ToArray()
            },
            DailyAnalysis = new
            {
                payload.DailyAnalysis.Success,
                payload.DailyAnalysis.ErrorCode,
                payload.DailyAnalysis.ErrorMessage,
                Analysis = payload.DailyAnalysis.Analysis is null
                    ? null
                    : new
                    {
                        payload.DailyAnalysis.Analysis.AnalysisId,
                        payload.DailyAnalysis.Analysis.CreatedAtUtc,
                        payload.DailyAnalysis.Analysis.ExecutiveSummary,
                        FunnelInsights = payload.DailyAnalysis.Analysis.FunnelInsights.Take(6).ToArray(),
                        LiquidityInsights = payload.DailyAnalysis.Analysis.LiquidityInsights.Take(6).ToArray(),
                        Risks = payload.DailyAnalysis.Analysis.Risks.Take(6).ToArray(),
                        RecommendedActions = payload.DailyAnalysis.Analysis.RecommendedActions.Take(6).ToArray()
                    }
            }
        };

        var prompt = BuildOpenAiPrompt(compactPayload);
        var gatewayResult = await growthAiGateway.GenerateAnalysisAsync(
            new AdminGrowthAiGatewayRequest(
                ApiKey: growthSettings.ApiKey,
                Model: growthSettings.Model,
                Temperature: growthSettings.Temperature,
                MaxOutputTokens: Math.Clamp(Math.Max(growthSettings.MaxOutputTokens, 1800), 400, 4000),
                SystemPrompt: growthSettings.SystemPrompt,
                UserPrompt: prompt),
            cancellationToken);

        if (!gatewayResult.Success || string.IsNullOrWhiteSpace(gatewayResult.OutputText))
        {
            _logger.LogWarning(
                "Hourly digest OpenAI generation failed. ErrorCode={ErrorCode} ErrorMessage={ErrorMessage}",
                gatewayResult.ErrorCode,
                gatewayResult.ErrorMessage);

            return BuildFallbackHtml(
                payload,
                $"Falha ao gerar relatorio HTML via OpenAI ({gatewayResult.ErrorCode ?? "unknown"}).");
        }

        var htmlFromAi = ExtractHtmlFromOutput(gatewayResult.OutputText);
        if (string.IsNullOrWhiteSpace(htmlFromAi))
        {
            return BuildFallbackHtml(payload, "OpenAI retornou conteudo vazio para o relatorio.");
        }

        return htmlFromAi;
    }

    private async Task SendReportEmailAsync(
        HourlyDigestPayload payload,
        string htmlReport,
        IAdminMailboxStore mailboxStore,
        IAdminMailboxGateway mailboxGateway,
        CancellationToken cancellationToken)
    {
        var snapshot = await mailboxStore.LoadAsync(cancellationToken);
        if (snapshot.Settings is null)
        {
            _logger.LogWarning("AdminGrowthAiHourlyDigestWorker skipped: mailbox settings not configured.");
            return;
        }

        var connection = BuildConnection(snapshot.Settings);
        var recipients = new List<string> { _primaryRecipient };
        recipients.AddRange(_ccRecipients);

        var distinctRecipients = recipients
            .Select(NormalizeEmail)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinctRecipients.Length == 0)
        {
            _logger.LogWarning("AdminGrowthAiHourlyDigestWorker skipped: no recipients configured.");
            return;
        }

        var generatedAtLocal = TimeZoneInfo.ConvertTimeFromUtc(payload.GeneratedAtUtc, ResolveTimeZone(_timeZoneId));
        var subject = $"{_subjectPrefix} - {generatedAtLocal:dd/MM/yyyy HH:mm}";

        foreach (var recipient in distinctRecipients)
        {
            await mailboxGateway.SendAsync(
                new AdminMailboxGatewaySendRequest(
                    Connection: connection,
                    To: recipient!,
                    Subject: subject,
                    Body: htmlReport,
                    IsHtml: true),
                cancellationToken);
        }
    }

    private static AdminMailboxGatewayConnection BuildConnection(AdminMailboxStoreSettings settings)
    {
        var username = NormalizeText(settings.Username, 320);
        var password = NormalizeText(settings.Password, 400);
        var senderEmail = NormalizeEmail(settings.SenderEmail) ?? NormalizeEmail(settings.Username);
        var senderDisplayName = NormalizeText(settings.SenderDisplayName, 120) ?? "ConsertaPraMim";
        var smtpHost = NormalizeText(settings.SmtpHost, 255);
        var pop3Host = NormalizeText(settings.Pop3Host, 255);

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(senderEmail) ||
            string.IsNullOrWhiteSpace(smtpHost) ||
            string.IsNullOrWhiteSpace(pop3Host))
        {
            throw new InvalidOperationException("Configuracao SMTP/POP3 incompleta para envio do relatorio horario.");
        }

        return new AdminMailboxGatewayConnection(
            Username: username,
            Password: password,
            SenderEmail: senderEmail,
            SenderDisplayName: senderDisplayName,
            SmtpHost: smtpHost,
            SmtpPort: settings.SmtpPort <= 0 ? 587 : settings.SmtpPort,
            SmtpUseSsl: settings.SmtpUseSsl,
            Pop3Host: pop3Host,
            Pop3Port: settings.Pop3Port <= 0 ? 995 : settings.Pop3Port,
            Pop3UseSsl: settings.Pop3UseSsl);
    }

    private static string BuildOpenAiPrompt(object payload)
    {
        var serializedPayload = JsonSerializer.Serialize(payload, JsonOptions);
        return
            """
            Gere um relatorio executivo em HTML para o ConsertaPraMim com base no JSON abaixo.

            Requisitos de formato:
            - Retorne somente HTML valido (sem markdown, sem blocos ```).
            - Estrutura com secoes claras e estilizadas: resumo executivo, KPIs principais, atividades recentes, monitoramento API, riscos, plano de acao das proximas 24h.
            - Use CSS inline no proprio HTML para cores e destaque visual.
            - Idioma: portugues-BR.
            - Tom objetivo de operacao/negocio.
            - Evite textos genericos; traga numeros concretos recebidos no payload.
            - Inclua semaforos visuais (verde/amarelo/vermelho) com base nos indicadores.

            JSON:
            """ + Environment.NewLine + serializedPayload;
    }

    private static string ExtractHtmlFromOutput(string outputText)
    {
        var trimmed = outputText.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineBreak = trimmed.IndexOf('\n');
            if (firstLineBreak >= 0)
            {
                trimmed = trimmed[(firstLineBreak + 1)..];
            }

            var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
            {
                trimmed = trimmed[..closingFence];
            }

            trimmed = trimmed.Trim();
        }

        if (trimmed.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("<section", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("<div", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return string.Empty;
    }

    private static string BuildFallbackHtml(HourlyDigestPayload payload, string warning)
    {
        static string Encode(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

        var sb = new StringBuilder();
        sb.AppendLine("<div style=\"font-family:Segoe UI,Arial,sans-serif;color:#111827;background:#f8fafc;padding:20px;\">");
        sb.AppendLine("<div style=\"max-width:960px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:14px;overflow:hidden;\">");
        sb.AppendLine("<div style=\"padding:16px 20px;background:linear-gradient(90deg,#0ea5e9,#2563eb);color:#ffffff;\">");
        sb.AppendLine("<h1 style=\"margin:0;font-size:20px;\">Relatorio horario ConsertaPraMim</h1>");
        sb.AppendLine($"<p style=\"margin:6px 0 0 0;font-size:13px;opacity:.95;\">Gerado em {Encode(payload.GeneratedAtUtc.ToString("dd/MM/yyyy HH:mm"))} UTC</p>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div style=\"padding:16px 20px;\">");
        sb.AppendLine($"<div style=\"background:#fef3c7;border:1px solid #f59e0b;color:#92400e;padding:10px 12px;border-radius:10px;margin-bottom:14px;\">{Encode(warning)}</div>");
        sb.AppendLine("<h2 style=\"font-size:16px;margin:0 0 10px 0;color:#0f172a;\">KPIs da Dashboard</h2>");
        sb.AppendLine("<ul style=\"margin:0 0 14px 18px;padding:0;line-height:1.7;\">");
        sb.AppendLine($"<li>Total de usuarios: <strong>{payload.Dashboard.TotalUsers}</strong></li>");
        sb.AppendLine($"<li>Pedidos no periodo: <strong>{payload.Dashboard.RequestsInPeriod}</strong></li>");
        sb.AppendLine($"<li>Propostas no periodo: <strong>{payload.Dashboard.ProposalsInPeriod}</strong> | Aceitas: <strong>{payload.Dashboard.AcceptedProposalsInPeriod}</strong></li>");
        sb.AppendLine($"<li>NPS operacional: <strong>{payload.Dashboard.OperationalNpsScore:F2}</strong> | Qualidade operacional: <strong>{payload.Dashboard.OperationalQualityScore:F2}</strong></li>");
        sb.AppendLine("</ul>");
        sb.AppendLine("<h2 style=\"font-size:16px;margin:0 0 10px 0;color:#0f172a;\">Monitoramento API</h2>");
        sb.AppendLine("<ul style=\"margin:0 0 14px 18px;padding:0;line-height:1.7;\">");
        sb.AppendLine($"<li>Requests: <strong>{payload.Monitoring.TotalRequests}</strong> | Erro: <strong>{payload.Monitoring.ErrorRatePercent:F2}%</strong></li>");
        sb.AppendLine($"<li>Latencia p95: <strong>{payload.Monitoring.P95LatencyMs} ms</strong> | RPM: <strong>{payload.Monitoring.RequestsPerMinute:F2}</strong></li>");
        sb.AppendLine($"<li>Top endpoint: <strong>{Encode(payload.Monitoring.TopEndpoint)}</strong></li>");
        sb.AppendLine($"<li>Health API/DB: <strong>{Encode(payload.Monitoring.ApiHealthStatus)}</strong> / <strong>{Encode(payload.Monitoring.DatabaseHealthStatus)}</strong></li>");
        sb.AppendLine("</ul>");
        sb.AppendLine("<h2 style=\"font-size:16px;margin:0 0 10px 0;color:#0f172a;\">Analise diaria de Growth AI</h2>");
        if (payload.DailyAnalysis.Success && payload.DailyAnalysis.Analysis is not null)
        {
            sb.AppendLine($"<p style=\"margin:0 0 8px 0;line-height:1.6;\">{Encode(payload.DailyAnalysis.Analysis.ExecutiveSummary)}</p>");
        }
        else
        {
            sb.AppendLine($"<p style=\"margin:0 0 8px 0;line-height:1.6;color:#b91c1c;\">Falha ao gerar analise diaria: {Encode(payload.DailyAnalysis.ErrorMessage)}</p>");
        }

        sb.AppendLine("<h2 style=\"font-size:16px;margin:12px 0 8px 0;color:#0f172a;\">Eventos recentes</h2>");
        sb.AppendLine("<ul style=\"margin:0 0 6px 18px;padding:0;line-height:1.7;\">");
        foreach (var evt in payload.Dashboard.RecentEvents.Take(10))
        {
            sb.AppendLine($"<li><strong>{Encode(evt.Title)}</strong> - {Encode(evt.Description)} ({evt.CreatedAt:dd/MM HH:mm})</li>");
        }

        sb.AppendLine("</ul>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");
        return sb.ToString();
    }

    private static DateTime CalculateNextTopOfHourUtc(DateTime nowUtc, TimeZoneInfo timeZone)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
        var nextLocal = new DateTime(
            nowLocal.Year,
            nowLocal.Month,
            nowLocal.Day,
            nowLocal.Hour,
            0,
            0,
            DateTimeKind.Unspecified);

        if (nextLocal < nowLocal)
        {
            nextLocal = nextLocal.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(nextLocal, timeZone);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
            }
            catch
            {
                // fallback below
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static IReadOnlyList<string> ParseRecipientList(string? raw, IReadOnlyList<string> fallback)
    {
        var parsed = (raw ?? string.Empty)
            .Split([';', ',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeEmail)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return parsed.Length == 0 ? fallback : parsed;
    }

    private static string? NormalizeText(string? raw, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (trimmed.Length > maxLength)
        {
            trimmed = trimmed[..maxLength];
        }

        return trimmed;
    }

    private static string? NormalizeEmail(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var candidate = raw.Trim();
        try
        {
            return new MailAddress(candidate).Address;
        }
        catch
        {
            return null;
        }
    }

    private static bool ParseBool(string? raw, bool defaultValue)
    {
        return bool.TryParse(raw, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static int ParseInt(string? raw, int defaultValue)
    {
        return int.TryParse(raw, out var parsed)
            ? parsed
            : defaultValue;
    }

    private sealed record HourlyDigestPayload(
        DateTime GeneratedAtUtc,
        DateTime WindowFromUtc,
        DateTime WindowToUtc,
        string TimeZoneId,
        AdminDashboardDto Dashboard,
        AdminMonitoringOverviewDto Monitoring,
        AdminGrowthAiAnalyzeResultDto DailyAnalysis,
        AdminGrowthAiAnalysisDto? LatestStoredAnalysis);
}
