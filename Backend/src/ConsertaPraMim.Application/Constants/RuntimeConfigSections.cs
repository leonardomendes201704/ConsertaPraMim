namespace ConsertaPraMim.Application.Constants;

public sealed record RuntimeConfigSectionDefinition(
    string SettingKey,
    string SectionPath,
    string DisplayName,
    string Description,
    string DefaultJson,
    bool RequiresRestart = true);

public static class RuntimeConfigSections
{
    public static readonly IReadOnlyList<RuntimeConfigSectionDefinition> All =
    [
        new RuntimeConfigSectionDefinition(
            SettingKey: SystemSettingKeys.ConfigDatabaseKeepAlive,
            SectionPath: "DatabaseKeepAlive",
            DisplayName: "Database KeepAlive",
            Description: "Configura worker de keepalive de conexao com banco.",
            DefaultJson:
            """
            {
              "Enabled": true,
              "IntervalSeconds": 60,
              "CommandText": "SELECT 1"
            }
            """),
        new RuntimeConfigSectionDefinition(
            SettingKey: SystemSettingKeys.ConfigSeed,
            SectionPath: "Seed",
            DisplayName: "Seed",
            Description: "Configura seed inicial da base. Em Reset=true fora de Development, exige codigo de seguranca no portal admin. Com SeedOperationalData=false, aplica somente seeds estruturais (sem usuarios/pedidos/telemetria fake).",
            DefaultJson:
            """
            {
              "Enabled": true,
              "Reset": false,
              "CreateDefaultAdmin": true,
              "SeedOperationalData": true,
              "DefaultPassword": "SeedDev!2026"
            }
            """),
        new RuntimeConfigSectionDefinition(
            SettingKey: SystemSettingKeys.ConfigServiceAppointments,
            SectionPath: "ServiceAppointments",
            DisplayName: "Service Appointments",
            Description: "Configura regras e workers de agendamentos.",
            DefaultJson:
            """
            {
              "ConfirmationExpiryHours": 12,
              "CancelMinimumHoursBeforeWindow": 2,
              "RescheduleMinimumHoursBeforeWindow": 2,
              "RescheduleMaximumAdvanceDays": 30,
              "EnableExpirationWorker": true,
              "ExpirationWorkerIntervalSeconds": 30,
              "ExpirationBatchSize": 200,
              "ScopeChanges": {
                "ClientApprovalTimeoutMinutes": 1440,
                "EnableExpirationWorker": true,
                "ExpirationWorkerIntervalSeconds": 30,
                "ExpirationBatchSize": 200
              },
              "Reminders": {
                "EnableWorker": true,
                "WorkerIntervalSeconds": 15,
                "BatchSize": 200,
                "MaxAttempts": 3,
                "RetryBaseDelaySeconds": 30,
                "OffsetsMinutes": [1440, 120, 30]
              },
              "Warranty": {
                "WindowDays": 30,
                "ProviderResponseSlaHours": 48,
                "EnableSlaWorker": true,
                "SlaWorkerIntervalSeconds": 30,
                "SlaEscalationBatchSize": 200
              },
              "NoShowRisk": {
                "EnableWorker": true,
                "WorkerIntervalSeconds": 30,
                "BatchSize": 200,
                "LookaheadHours": 24,
                "IncludePastWindowMinutes": 30,
                "OperationalAlerts": {
                  "Enabled": true,
                  "EvaluationWindowHours": 24,
                  "CancellationNoShowWindowHours": 24,
                  "CooldownMinutes": 30,
                  "Recipients": []
                }
              }
            }
            """),
        new RuntimeConfigSectionDefinition(
            SettingKey: SystemSettingKeys.ConfigProviderGallery,
            SectionPath: "ProviderGallery",
            DisplayName: "Provider Gallery",
            Description: "Configura retencao e limpeza de evidencias.",
            DefaultJson:
            """
            {
              "EvidenceRetention": {
                "EnableWorker": true,
                "WorkerIntervalMinutes": 60,
                "RetentionDays": 180,
                "BatchSize": 200
              }
            }
            """),
        new RuntimeConfigSectionDefinition(
            SettingKey: SystemSettingKeys.ConfigPayments,
            SectionPath: "Payments",
            DisplayName: "Payments",
            Description: "Configura provider de pagamento e parametros mock.",
            DefaultJson:
            """
            {
              "Provider": "Mock",
              "Mock": {
                "CheckoutBaseUrl": "https://checkout.consertapramim.com/pay",
                "WebhookSecret": "mock-secret-dev",
                "SessionExpiryMinutes": 30
              }
            }
            """),
        new RuntimeConfigSectionDefinition(
            SettingKey: SystemSettingKeys.ConfigPushNotifications,
            SectionPath: "PushNotifications",
            DisplayName: "Push Notifications",
            Description: "Configura envio push (Firebase).",
            DefaultJson:
            """
            {
              "Firebase": {
                "ProjectId": "consertapramimcliente",
                "ServiceAccountPath": "C:\\\\secrets\\\\firebase\\\\consertapramimcliente-firebase-adminsdk-fbsvc-eb0eb9ce32.json",
                "ServerKey": ""
              }
            }
            """),
        new RuntimeConfigSectionDefinition(
            SettingKey: SystemSettingKeys.ConfigAdminPortals,
            SectionPath: "AdminPortals",
            DisplayName: "Admin Portals",
            Description: "Configura os links de portal exibidos no menu admin e usados nos health checks de dependencias, com suporte a ambiente Local/Vps.",
            DefaultJson:
            """
            {
              "Target": "auto",
              "ClientUrl": "http://localhost:5069/",
              "ProviderUrl": "http://localhost:5140/",
              "AdminUrl": "http://localhost:5151/",
              "Environments": {
                "Local": {
                  "ClientUrl": "http://localhost:5069/",
                  "ProviderUrl": "http://localhost:5140/",
                  "AdminUrl": "http://localhost:5151/"
                },
                "Vps": {
                  "ClientUrl": "http://localhost:5069/",
                  "ProviderUrl": "http://localhost:5140/",
                  "AdminUrl": "http://localhost:5151/"
                }
              }
            }
            """,
            RequiresRestart: false),
        new RuntimeConfigSectionDefinition(
            SettingKey: SystemSettingKeys.ConfigSwagger,
            SectionPath: "Swagger",
            DisplayName: "Swagger",
            Description: "Controla exposicao do Swagger/OpenAPI na API em ambiente Production.",
            DefaultJson:
            """
            {
              "EnabledInProduction": false
            }
            """),
        new RuntimeConfigSectionDefinition(
            SettingKey: SystemSettingKeys.ConfigMonitoring,
            SectionPath: "Monitoring",
            DisplayName: "Monitoring",
            Description: "Configura telemetria, captura de contexto e workers de agregacao.",
            DefaultJson:
            """
            {
              "Enabled": true,
              "CaptureSwaggerRequests": true,
              "IpHashSalt": "conserta-monitoring-default",
              "BodyCapture": {
                "CaptureRequestBody": true,
                "CaptureResponseBody": true,
                "MaxBodyChars": 4000
              },
              "ContextCapture": {
                "CaptureHeaders": true,
                "CaptureQueryString": true,
                "CaptureRouteValues": true,
                "MaxContextChars": 8000
              },
              "DependencyHealth": {
                "TimeoutMs": 3000,
                "CacheSeconds": 15
              },
              "Realtime": {
                "Enabled": true,
                "MinIntervalSeconds": 3
              },
              "TelemetryBuffer": {
                "Capacity": 30000
              },
              "FlushWorker": {
                "Enabled": true,
                "BatchSize": 250,
                "AccumulateDelayMs": 150
              },
              "AggregationWorker": {
                "Enabled": true,
                "IntervalSeconds": 45,
                "HourlyRecomputeWindowHours": 72,
                "DailyRecomputeWindowDays": 45
              },
              "Retention": {
                "RawDays": 14,
                "AggregateDays": 180
              }
            }
            """)
        ,
        new RuntimeConfigSectionDefinition(
            SettingKey: SystemSettingKeys.ConfigLandingAnalytics,
            SectionPath: "LandingAnalytics",
            DisplayName: "Landing Analytics",
            Description: "Configura heartbeat, heatmap fase 1, scroll milestones e GeoIP da landing publica.",
            DefaultJson:
            """
            {
              "ClientTelemetryEnabled": true,
              "Heartbeat": {
                "Enabled": true,
                "IntervalSeconds": 15,
                "MaxSessionDurationMinutes": 30
              },
              "Scroll": {
                "Enabled": true,
                "MilestonesPercent": [25, 50, 75, 100]
              },
              "Clicks": {
                "Enabled": true,
                "TrackInteractiveOnly": true,
                "HeatmapGridRows": 6,
                "HeatmapGridColumns": 6
              },
              "GeoIp": {
                "Enabled": true,
                "Provider": "ipwhois",
                "BaseUrl": "https://ipwho.is",
                "TimeoutMs": 1200,
                "CacheMinutes": 1440
              }
            }
            """,
            RequiresRestart: false),
        new RuntimeConfigSectionDefinition(
            SettingKey: SystemSettingKeys.ConfigFireTvDashboard,
            SectionPath: "FireTvDashboard",
            DisplayName: "Fire TV Dashboard",
            Description: "Configura o dashboard read-only da landing exibido no app Fire TV, incluindo KPIs, janelas e auto refresh.",
            DefaultJson:
            """
            {
              "Enabled": true,
              "AppTitle": "ConsertaPraMim Analytics TV",
              "AppSubtitle": "Landing publica",
              "DefaultRangeDays": 7,
              "AllowedRangeDays": [1, 7, 30],
              "DefaultOriginFilter": "all",
              "OriginFilters": [
                { "Value": "all", "Label": "Todas as origens" },
                { "Value": "client", "Label": "Cliente" },
                { "Value": "provider", "Label": "Prestador" }
              ],
              "DefaultComparisonMode": "previous_period",
              "ComparisonModes": [
                { "Value": "none", "Label": "Sem comparacao" },
                { "Value": "previous_period", "Label": "Periodo anterior" }
              ],
              "AutoRefreshSeconds": 30,
              "SessionPageSize": 6,
              "TopListSize": 5,
              "ShowHeatmap": true,
              "ShowComparison": true,
              "ShowScrollmap": true,
              "ShowElementRanking": true,
              "ElementRankingSize": 6,
              "KpiKeys": [
                "totalSessions",
                "uniqueVisitors",
                "leadSubmissions",
                "leadSubmissionRatePercent",
                "leadModalOpens",
                "totalClicks",
                "averageActiveSecondsPerSession",
                "averageMaxScrollPercent"
              ]
            }
            """,
            RequiresRestart: false)
    ];

    private static readonly Dictionary<string, RuntimeConfigSectionDefinition> BySettingKey =
        All.ToDictionary(x => x.SettingKey, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, RuntimeConfigSectionDefinition> BySectionPath =
        All.ToDictionary(x => x.SectionPath, StringComparer.OrdinalIgnoreCase);

    public static bool TryGetBySettingKey(string? settingKey, out RuntimeConfigSectionDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(settingKey))
        {
            definition = null!;
            return false;
        }

        return BySettingKey.TryGetValue(settingKey.Trim(), out definition!);
    }

    public static bool TryGetBySectionPath(string? sectionPath, out RuntimeConfigSectionDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(sectionPath))
        {
            definition = null!;
            return false;
        }

        return BySectionPath.TryGetValue(sectionPath.Trim(), out definition!);
    }
}
