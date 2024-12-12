using System.Collections.Concurrent;
using WebPeli.GameEngine;

public class MessageCapturingLogger : ILogger
{
    private readonly int MAX_MESSAGES = Config.LOG_MAX_MESSAGES; // Configurable max size
    private readonly string _categoryName;
    private readonly ConcurrentQueue<LogMessage> _messages = new();
    
    public MessageCapturingLogger(string categoryName)
    {
        _categoryName = categoryName;
    }

    public IEnumerable<LogMessage> Messages => _messages;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = new LogMessage(_categoryName, DateTime.UtcNow, logLevel, formatter(state, exception), exception);
        _messages.Enqueue(message);
        
        // Basic cleanup - keep queue size in check
        while (_messages.Count > MAX_MESSAGES)
        {
            _messages.TryDequeue(out _);
        }
    }

    public bool IsEnabled(LogLevel logLevel) => true;
    
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => 
        new NoOpDisposable();

    private class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
public record LogMessage(string? CategoryName, DateTime Timestamp, LogLevel Level, string? Message, Exception? Exception);