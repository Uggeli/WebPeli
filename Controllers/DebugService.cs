using System.Net.WebSockets;
using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Systems;
using WebPeli.Network;

namespace WebPeli.GameEngine;

public class DebugDataService(
    ILogger<DebugDataService> logger,
    ViewportManager viewportManager,
    EntityRegister entityRegister,
    MovementSystem movementSystem)
{
    private readonly Dictionary<string, int> _systemUpdateTimes = [];
    private readonly HashSet<WebSocket> _debugSockets = [];
    private readonly ILogger<DebugDataService> _logger = logger;
    private readonly ViewportManager _viewportManager = viewportManager;
    private readonly EntityRegister _entityRegister = entityRegister;
    private readonly MovementSystem _movementSystem = movementSystem;

    private bool _isRunning = false;
    private readonly object _updateLock = new();

    public void RegisterDebugSocket(WebSocket socket)
    {
        _debugSockets.Add(socket);
        _logger.LogInformation("Debug socket registered. Total sockets: {Count}", _debugSockets.Count);
    }

    public void UnregisterDebugSocket(WebSocket socket)
    {
        _debugSockets.Remove(socket);
        _logger.LogInformation("Debug socket unregistered. Total sockets: {Count}", _debugSockets.Count);
    }

    public void UpdateSystemTime(string systemName, int milliseconds)
    {
        lock (_updateLock)
        {
            _systemUpdateTimes[systemName] = milliseconds;
        }
    }

    private DebugState CollectDebugState()
    {
        Dictionary<string, int> times;
        lock (_updateLock)
        {
            times = new Dictionary<string, int>(_systemUpdateTimes);
        }

        return new DebugState
        {
            // Time system data
            Season = TimeSystem.CurrentSeason,
            TimeOfDay = TimeSystem.CurrentTimeOfDay,
            Day = TimeSystem.CurrentDay,
            Year = TimeSystem.CurrentYear,
            
            // Entity stats 
            // TODO: Add these to EntityRegister
            TotalEntities = 0, // _entityRegister.TotalEntities,
            ActiveEntities = 0, // _entityRegister.ActiveEntities,
            MovingEntities = 0, // _movementSystem.MovingEntitiesCount,
            
            // System status
            DebugMode = Config.DebugMode,
            PathfindingDebug = Config.DebugPathfinding,
            ActiveViewports = _viewportManager._activeViewports.Count,
            
            // Performance data
            SystemUpdateTimes = times
        };
    }

    public async Task StartDebugLoop(CancellationToken cancellationToken)
    {
        if (_isRunning) return;
        _isRunning = true;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Remove closed sockets
                _debugSockets.RemoveWhere(s => s.State != WebSocketState.Open);

                if (_debugSockets.Count == 0)
                {
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                var state = CollectDebugState();
                var data = MessageProtocol.EncodeDebugData(state);

                var sendTasks = _debugSockets.Select(async socket =>
                {
                    try
                    {
                        if (socket.State == WebSocketState.Open)
                        {
                            await socket.SendAsync(
                                new ArraySegment<byte>(data),
                                WebSocketMessageType.Binary,
                                true,
                                cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending debug data to socket");
                    }
                });

                await Task.WhenAll(sendTasks);
                await Task.Delay(1000, cancellationToken); // Update once per second
            }
        }
        finally
        {
            _isRunning = false;
        }
    }
}