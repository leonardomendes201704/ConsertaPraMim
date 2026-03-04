using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace ConsertaPraMim.Tests.Unit.Services;

public class TelegramChatbotObservabilityServiceTests
{
    [Fact(DisplayName = "Telegram chatbot observability | Snapshot | Deve consolidar metricas de trafego, IA e dependencias")]
    public void GetSnapshot_ShouldAggregateMetrics()
    {
        var service = new TelegramChatbotObservabilityService(new FakeWebHostEnvironment("Development"));

        service.RecordInboundMessage(attachmentCount: 2);
        service.RecordOutboundMessage();
        service.RecordDependency("openai.responses", success: true, latencyMilliseconds: 120);
        service.RecordDependency("api.telegram_chatbot.orders", success: false, latencyMilliseconds: 90, errorCode: "query_orders_failed");
        service.RecordBusinessEvent("triage_request_opened", success: true);
        service.RecordBusinessEvent("scheduling_attempt", success: false);
        service.RecordIncident("openai_gateway", "openai_unavailable", "corr-1", "timeout");

        service.RecordAiOutcome(
            new TelegramChatbotAssistantReply(
                MessageText: "Fallback seguro",
                Intent: "human_handoff",
                NextStep: "handoff_human_provider",
                Confidence: null,
                EntitiesJson: "{}",
                UsedFallback: true,
                UsedCache: false,
                PromptTokens: 100,
                CompletionTokens: 50,
                TotalTokens: 150,
                ModelName: "gpt-4.1-mini",
                PromptVersion: "st-010-v1",
                CorrelationId: "corr-1",
                LatencyMilliseconds: 120),
            new TelegramAiGatewayResult(
                Success: false,
                ErrorCode: "openai_unavailable",
                ErrorMessage: "timeout",
                InputTokens: 100,
                OutputTokens: 50,
                TotalTokens: 150,
                AttemptCount: 2,
                LatencyMilliseconds: 120));

        var snapshot = service.GetSnapshot();

        Assert.Equal("Development", snapshot.Environment);
        Assert.Equal(1, snapshot.Traffic.InboundMessages);
        Assert.Equal(1, snapshot.Traffic.OutboundMessages);
        Assert.Equal(1, snapshot.Traffic.MessagesWithAttachments);

        Assert.Equal(1, snapshot.Ai.Requests);
        Assert.Equal(1, snapshot.Ai.Fallbacks);
        Assert.Equal(1, snapshot.Ai.Failures);
        Assert.True(snapshot.Ai.Tokens >= 150);
        Assert.True(snapshot.Ai.HumanHandoffs >= 1);

        Assert.Equal(1, snapshot.Business.TriageRequestsOpened);
        Assert.Equal(1, snapshot.Business.SchedulingAttempts);
        Assert.Equal(1, snapshot.Business.SchedulingFailures);

        Assert.Contains(snapshot.Dependencies, item => item.Dependency == "openai.responses");
        Assert.Contains(snapshot.TopErrors, item => item.ErrorCode == "openai_unavailable");
        Assert.NotEmpty(snapshot.RecentIncidents);
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public FakeWebHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
            ApplicationName = "ConsertaPraMim.Tests";
            ContentRootPath = AppContext.BaseDirectory;
            WebRootPath = AppContext.BaseDirectory;
            ContentRootFileProvider = new NullFileProvider();
            WebRootFileProvider = new NullFileProvider();
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; }

        public string WebRootPath { get; set; }

        public IFileProvider WebRootFileProvider { get; set; }

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
