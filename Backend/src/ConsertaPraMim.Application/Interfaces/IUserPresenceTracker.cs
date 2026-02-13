namespace ConsertaPraMim.Application.Interfaces;

public interface IUserPresenceTracker
{
    UserPresenceChangedEvent? RegisterConnection(string connectionId, Guid userId);
    UserPresenceChangedEvent? UnregisterConnection(string connectionId);
    bool IsOnline(Guid userId);
    int CountOnlineUsers(IEnumerable<Guid> userIds);
}

public record UserPresenceChangedEvent(Guid UserId, bool IsOnline, DateTime UpdatedAtUtc);
