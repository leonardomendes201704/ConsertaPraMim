using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramChatbotObservabilityService
{
    void RecordInboundMessage(int attachmentCount);

    void RecordOutboundMessage();

    void RecordAiOutcome(TelegramChatbotAssistantReply reply, TelegramAiGatewayResult gatewayResult);

    void RecordBusinessEvent(string eventName, bool success);

    void RecordDependency(string dependency, bool success, long latencyMilliseconds, string? errorCode = null);

    void RecordIncident(string stage, string errorCode, string? correlationId, string? message);

    TelegramChatbotObservabilitySnapshotDto GetSnapshot();
}
