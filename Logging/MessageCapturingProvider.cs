using System.Collections.Concurrent;

namespace WebPeli.Logging;

public class MessageCapturingProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, MessageCapturingLogger> _loggers = new();
    private DateTime _lastFetchTime = DateTime.UtcNow;
    public IEnumerable<LogMessage> GetFilteredMessages(
        string? categoryName = null,
        DateTime? since = null,
        LogLevel? minLevel = null,
        int? limit = null)
    {
        var messages = categoryName != null 
            ? _loggers.TryGetValue(categoryName, out var logger) ? logger.Messages : Enumerable.Empty<LogMessage>()
            : _loggers.Values.SelectMany(logger => logger.Messages);

        if (since.HasValue)
            messages = messages.Where(m => m.Timestamp >= since.Value);
            
        if (minLevel.HasValue)
            messages = messages.Where(m => m.Level >= minLevel.Value);

        messages = messages.OrderByDescending(m => m.Timestamp);
        
        if (limit.HasValue)
            messages = messages.Take(limit.Value);
            
        return messages;
    }
    public IEnumerable<LogMessage> GetNewMessages(string? categoryName = null)
    {
        var lastFetch = _lastFetchTime;
        _lastFetchTime = DateTime.UtcNow;

        if (categoryName != null)
        {
            if (_loggers.TryGetValue(categoryName, out var logger))
            {
                return logger.Messages.Where(m => m.Timestamp > lastFetch);
            }
            return [];
        }

        return _loggers.Values
            .SelectMany(logger => logger.Messages)
            .Where(m => m.Timestamp > lastFetch)
            .OrderBy(m => m.Timestamp);
    }

    public IEnumerable<string> GetCategories() => _loggers.Keys;

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new MessageCapturingLogger(name));
    }

    public void Dispose()
    {
        _loggers.Clear();
    }

    // Method to get all messages from all loggers
    public IEnumerable<LogMessage> GetAllMessages()
    {
        return _loggers.Values.SelectMany(logger => logger.Messages);
    }

    // Method to get messages for a specific category (class)
    public IEnumerable<LogMessage> GetMessagesForCategory(string categoryName)
    {
        if (_loggers.TryGetValue(categoryName, out var logger))
        {
            return logger.Messages;
        }
        return [];
    }
}

