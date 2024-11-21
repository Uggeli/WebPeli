using System.Buffers.Binary;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using WebPeli.GameEngine.Managers;

namespace WebPeli.Transport;

public class WebSocketTransport(WebSocket webSocket, ViewportManager viewportManager, ILogger<WebSocketTransport> logger) : GameTransportBase(viewportManager, logger), IGameTransport
{
    private readonly WebSocket _webSocket = webSocket;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private Memory<byte>? _lastViewportRequest;
    private readonly TimeSpan _viewportUpdateInterval = TimeSpan.FromMilliseconds(16); 

    public async Task StartAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("WebSocket transport started");

        var receiveTask = ReceiveLoopAsync(ct);
        var updateTask = UpdateLoopAsync(ct);

        await Task.WhenAny(receiveTask, updateTask);

        await StopAsync(ct);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            var buffer = new byte[MaxMessageSize];
            var receiveResult = await _webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), ct);

            while (!receiveResult.CloseStatus.HasValue)
            {
                if (receiveResult.MessageType == WebSocketMessageType.Binary)
                {
                    var messageData = new Memory<byte>(buffer, 0, receiveResult.Count);
                    await HandleMessageAsync(messageData);
                }

                
                receiveResult = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), ct);
            }

            await _webSocket.CloseAsync(
                receiveResult.CloseStatus.Value,
                receiveResult.CloseStatusDescription,
                ct);
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
            _logger.LogInformation("WebSocket transport stopped");
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WebSocket transport");
            throw;
        }
    }

    private async Task UpdateLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_lastViewportRequest is not null)
                {
                    await HandleViewportRequestAsync(_lastViewportRequest.Value, SendMessageWrapper);
                }
                await Task.Delay(_viewportUpdateInterval, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WebSocket transport update loop");
            throw;
        }
    }

    private async Task HandleMessageAsync(Memory<byte> messageData)
    {
        if (messageData.Length < 3)
        {
            await SendErrorAsync(SendMessageWrapper, 0x04, "Invalid message format");
            return;
        }

        var messageType = (MessageType)messageData.Span[0];
        var length = BinaryPrimitives.ReadUInt16LittleEndian(messageData.Span[1..]);

        if (length > MaxMessageSize - 3)
        {
            await SendErrorAsync(SendMessageWrapper, 0x04, "Message too large");
            return;
        }

        var payload = messageData.Slice(3, length);

        switch (messageType)
        {
            case MessageType.ViewportRequest:
                _lastViewportRequest = payload;
                await HandleViewportRequestAsync(payload, SendMessageWrapper);
                break;
            default:
                await SendErrorAsync(SendMessageWrapper, 0x04, "Unknown message type");
                break;
        }
    }

    private async Task SendViewportUpdateAsync()
    {
        if (_lastViewportRequest is not null)
        {
            await HandleViewportRequestAsync(_lastViewportRequest.Value, SendMessageWrapper);
        }
    }

    // Wrapper to match delegate pattern
    private Task SendMessageWrapper(MessageType type, ReadOnlyMemory<byte> payload)
    {
        return SendMessageAsync(type, payload, CancellationToken.None);
    }

    public async Task SendMessageAsync(MessageType type, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        var message = EncodeMessage(type, payload);

        // Ensure only one send at a time
        await _sendLock.WaitAsync(ct);
        try
        {
            await _webSocket.SendAsync(message, WebSocketMessageType.Binary, true, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        try
        {
            _cts.Cancel();
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                    "Server shutting down", ct);
            }
        }
        finally
        {
            _cts.Dispose();
            _sendLock.Dispose();
        }
    }

    // Helper method to map WebSocket to our transport
    public static async Task HandleWebSocketRequest(HttpContext context,
        ViewportManager viewportManager,
        ILogger<WebSocketTransport> logger)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        logger.LogInformation("WebSocket connection established");
        var transport = new WebSocketTransport(webSocket, viewportManager, logger);

        await transport.StartAsync(context.RequestAborted);
    }
}