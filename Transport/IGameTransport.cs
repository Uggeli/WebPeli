namespace WebPeli.Transport;

public interface IGameTransport 
{
    // Core message handling
    Task SendMessageAsync(MessageType type, ReadOnlyMemory<byte> payload, CancellationToken ct = default);
    
    // Lifecycle
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
