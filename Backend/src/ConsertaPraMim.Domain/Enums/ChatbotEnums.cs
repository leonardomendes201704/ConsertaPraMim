namespace ConsertaPraMim.Domain.Enums;

public enum ChatbotConversationStatus
{
    Active = 1,
    Closed = 2
}

public enum ChatbotMessageDirection
{
    Incoming = 1,
    Outgoing = 2,
    System = 3
}

public enum ChatbotActionStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3
}
