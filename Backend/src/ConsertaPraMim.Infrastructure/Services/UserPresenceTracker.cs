using ConsertaPraMim.Application.Interfaces;
using System.Collections.Concurrent;

namespace ConsertaPraMim.Infrastructure.Services;

public class UserPresenceTracker : IUserPresenceTracker
{
    private readonly ConcurrentDictionary<string, Guid> _connectionUsers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, int> _userConnectionCounts = new();

    public UserPresenceChangedEvent? RegisterConnection(string connectionId, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(connectionId) || userId == Guid.Empty)
        {
            return null;
        }

        _connectionUsers[connectionId] = userId;
        var count = _userConnectionCounts.AddOrUpdate(userId, 1, (_, current) => current + 1);
        if (count != 1)
        {
            return null;
        }

        return new UserPresenceChangedEvent(userId, true, DateTime.UtcNow);
    }

    public UserPresenceChangedEvent? UnregisterConnection(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return null;
        }

        if (!_connectionUsers.TryRemove(connectionId, out var userId))
        {
            return null;
        }

        var count = _userConnectionCounts.AddOrUpdate(userId, 0, (_, current) => current > 0 ? current - 1 : 0);
        if (count > 0)
        {
            return null;
        }

        _userConnectionCounts.TryRemove(userId, out _);
        return new UserPresenceChangedEvent(userId, false, DateTime.UtcNow);
    }

    public bool IsOnline(Guid userId)
    {
        return _userConnectionCounts.TryGetValue(userId, out var count) && count > 0;
    }

    public int CountOnlineUsers(IEnumerable<Guid> userIds)
    {
        if (userIds == null)
        {
            return 0;
        }

        var count = 0;
        foreach (var userId in userIds.Where(id => id != Guid.Empty).Distinct())
        {
            if (IsOnline(userId))
            {
                count++;
            }
        }

        return count;
    }
}
