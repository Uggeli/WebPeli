using System.Net.WebSockets;
using Microsoft.AspNetCore.Mvc;
using WebPeli.GameEngine;
using WebPeli.GameEngine.Managers;
using WebPeli.Network;

namespace WebPeli.Network;

public class GameSocketHandler(ILogger<GameSocketHandler> logger) : ControllerBase
{
    private readonly ILogger<GameSocketHandler> _logger = logger;
    private const int MaxMessageSize = 64 * 1024; // 64KB

    [Route("/ws")]
    public async Task Get()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = 400;
            return;
        }

        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await HandleWebSocketConnection(webSocket);
    }
    
    private async Task HandleWebSocketConnection(WebSocket webSocket)
    {
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
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing as requested by the client",
                        CancellationToken.None);
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
    }
    
    private async Task HandleMessage(WebSocket webSocket, byte[] messageData)
    {
        try
        {
            if (!MessageProtocol.TryDecodeMessage(messageData, out var type, out var payload))
            {
                await SendError(webSocket, "Invalid message format");
                return;
            }
            var payloadBytes = payload.ToArray();

            switch (type)
            {
                case MessageType.ViewportRequest:
                    await HandleViewportRequest(webSocket, payloadBytes);
                    break;
                    
                case MessageType.CellInfo:
                    // Future implementation
                    await SendError(webSocket, "CellInfo not implemented yet");
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

    private static async Task HandleViewportRequest(WebSocket webSocket, byte[] payload)
    {
        if (!MessageProtocol.TryDecodeViewportRequest(payload.AsSpan(), out var cameraX, out var cameraY, 
            out var width, out var height))
        {
            await SendError(webSocket, "Invalid viewport request format");
            return;
        }

        // Create a TaskCompletionSource for the viewport data
        var tcs = new TaskCompletionSource<ViewportDataBinary>();
        
        // Register callback
        var callbackId = EventManager.RegisterCallback((ViewportDataBinary data) => {
            tcs.SetResult(data);
        });

        // Request viewport data
        EventManager.Emit(new ViewportRequest {
            CameraX = cameraX,
            CameraY = cameraY,
            ViewportWidth = width,
            ViewportHeight = height,
            CallbackId = callbackId
        });

        // Wait for response
        var viewportData = await tcs.Task;
        
        // Get tile data from viewport data
        var (tileWidth, tileHeight) = viewportData.GetDimensions();
        var tileGrid = new byte[tileWidth, tileHeight];
        var tileData = viewportData.EncodedData.Span.Slice(4); // Skip dimensions
        
        for (int y = 0; y < tileHeight; y++)
            for (int x = 0; x < tileWidth; x++)
                tileGrid[x, y] = tileData[y * tileWidth + x];

        // Encode and send response
        var response = MessageProtocol.EncodeViewportData(tileGrid);
        await webSocket.SendAsync(
            new ArraySegment<byte>(response),
            WebSocketMessageType.Binary,
            true,
            CancellationToken.None);
    }
    
    private static async Task SendError(WebSocket webSocket, string message)
    {
        var errorMessage = MessageProtocol.EncodeError(message);
        await webSocket.SendAsync(
            new ArraySegment<byte>(errorMessage),
            WebSocketMessageType.Binary,
            true,
            CancellationToken.None);
    }
}