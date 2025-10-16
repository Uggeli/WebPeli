using System.Net.WebSockets;
using Microsoft.AspNetCore.Mvc;
using WebPeli.GameEngine;

namespace WebPeli.Network;

public class DebugSocketHandler(ILogger<DebugSocketHandler> logger, DebugDataService debugDataService) : ControllerBase
{
    private readonly DebugDataService _debugDataService = debugDataService;
    private readonly ILogger<DebugSocketHandler> _logger = logger;
    private const int MaxMessageSize = 640 * 1024; // 64KB

    [Route("/debugws")]
    public async Task Get()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = 400;
            return;
        }
        System.Console.WriteLine("Debug socket connected");
        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await HandleWebSocketConnection(webSocket);
    }

    private async Task HandleWebSocketConnection(WebSocket webSocket)
    {
        _debugDataService.RegisterDebugSocket(webSocket);


        var buffer = new byte[MaxMessageSize];
        var receiveBuffer = new List<byte>();

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing as requested by the client", CancellationToken.None);
                    break;
                }

                // Accumulate received data
                receiveBuffer.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));

                // If we have a complete message
                if (result.EndOfMessage)
                {
                    await HandleMessage(webSocket, receiveBuffer.ToArray());
                    receiveBuffer.Clear();
                }
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogError(ex, "WebSocket error occurred");
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.InternalServerError,
                    "Internal server error",
                    CancellationToken.None);
            }
        }
        finally
        {
            _debugDataService.UnregisterDebugSocket(webSocket);
        }
    }

    private static (bool success, MessageType type, byte[] payload) DecodeMessage(byte[] messageData)
    {
        ReadOnlySpan<byte> dataSpan = messageData;
        if (!MessageProtocol.TryDecodeMessage(dataSpan, out var type, out var payload))
        {
            return (false, default, Array.Empty<byte>());
        }
        return (true, type, payload.ToArray());
    }
    
    private async Task HandleMessage(WebSocket webSocket, byte[] messageData)
    {
        try
        {
            var (success, type, payloadBytes) = DecodeMessage(messageData);
            if (!success)
            {
                await SendError(webSocket, "Invalid message format");
                return;
            }

            switch (type)
            {
                case MessageType.DebugRequest:
                    await HandleDebugRequest(webSocket, payloadBytes);
                    break;

                default:
                    await SendError(webSocket, $"Unknown message type: {type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message");
            await SendError(webSocket, "Internal server error");
        }

    }

    private async Task HandleDebugRequest(WebSocket webSocket, byte[] payload)
    {
        if (!MessageProtocol.TryDecodeDebugRequest(payload.AsSpan(), out var debugRequestType))
        {
            await SendError(webSocket, "Invalid debug request format");
            return;
        }

        switch (debugRequestType)
        {
            case DebugRequestType.RequestFullLog:
                // Just get messages for the category - we can enhance filtering later
                var messages = _debugDataService.GetMessagesForCategory("WebPeli");
                var data = MessageProtocol.EncodeLogMessages(messages);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(data),
                    WebSocketMessageType.Binary,
                    true,
                    CancellationToken.None
                );
                break;

            case DebugRequestType.ToggleDebugMode:
                Config.DebugMode = !Config.DebugMode;
                await SendDebugResponse(webSocket, "Debug mode toggled");
                break;

            case DebugRequestType.TogglePathfinding:
                Config.DebugPathfinding = !Config.DebugPathfinding;
                await SendDebugResponse(webSocket, "Pathfinding debug toggled");
                break;

            case DebugRequestType.RequestFullState:
                await SendDebugResponse(webSocket, "Full state requested, Not implemented");
                break;

            default:
                await SendError(webSocket, $"Unknown debug request type: {debugRequestType}");
                break;
        }
    }

    private static async Task SendDebugResponse(WebSocket webSocket, string message)
    {
        var response = MessageProtocol.EncodeDebugResponse(message);
        await webSocket.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    private static async Task SendError(WebSocket webSocket, string message)
    {
        var errorMessage = MessageProtocol.EncodeError(message);
        await webSocket.SendAsync(new ArraySegment<byte>(errorMessage), WebSocketMessageType.Binary, true, CancellationToken.None);
    }
}