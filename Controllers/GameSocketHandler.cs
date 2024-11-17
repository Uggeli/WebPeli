using System.Net.WebSockets;
using Microsoft.AspNetCore.Mvc;
using WebPeli.GameEngine.Managers;
using WebPeli.Transport;

public class GameSocketHandler(ViewportManager viewportManager, ILogger<GameSocketHandler> logger, ILoggerFactory loggerFactory) : ControllerBase
{
    private readonly ViewportManager _viewportManager = viewportManager;
    private readonly ILogger<GameSocketHandler> _logger = logger;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    [Route("/ws")]
    public async Task Get()
    {
        _logger.LogInformation("WebSocket request received");
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = 400;
            return;
        }

        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        _logger.LogInformation("WebSocket connection established");

        try 
        {
            // Keep transport in this scope
            var transportLogger = _loggerFactory.CreateLogger<WebSocketTransport>();
            var transport = new WebSocketTransport(webSocket, _viewportManager, transportLogger);
                
            await transport.StartAsync(HttpContext.RequestAborted);

            // Wait for the transport to finish
            while (webSocket.State == WebSocketState.Open)
            {
                await Task.Delay(100);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WebSocket connection");
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.InternalServerError,
                    "Internal error",
                    CancellationToken.None);
            }
        }
    }
}