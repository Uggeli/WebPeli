using System.Net.WebSockets;
using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Systems;
using WebPeli.Network;

namespace WebPeli.GameEngine;

public class DebugDataService
{
    private readonly Dictionary<string, int> _systemUpdateTimes = new();
    private readonly HashSet<WebSocket> _debugSockets = new();
    private readonly object _updateLock = new();
    
    // System references
    private readonly ILogger<DebugDataService> _logger;
    private readonly ViewportManager _viewportManager;
    private readonly EntityRegister _entityRegister;
    private readonly MovementSystem _movementSystem;
    private readonly TimeSystem _timeSystem;
    private readonly MetabolismSystem _metabolismSystem;
    private readonly VegetationSystem _vegetationSystem;
    private readonly AiManager _aiManager;
    private bool _isRunning = false;

    public DebugDataService(
        ILogger<DebugDataService> logger,
        ViewportManager viewportManager,
        EntityRegister entityRegister,
        MovementSystem movementSystem,
        TimeSystem timeSystem,
        MetabolismSystem metabolismSystem,
        VegetationSystem vegetationSystem,
        AiManager aiManager)
    {
        _logger = logger;
        _viewportManager = viewportManager;
        _entityRegister = entityRegister;
        _movementSystem = movementSystem;
        _timeSystem = timeSystem;
        _metabolismSystem = metabolismSystem;
        _vegetationSystem = vegetationSystem;
        _aiManager = aiManager;
    }

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

            // TODO: Add these to EntityRegister
            TotalEntities = 0,
            ActiveEntities = 0,
            MovingEntities = 0,

            // System status
            DebugMode = Config.DebugMode,
            PathfindingDebug = Config.DebugPathfinding,
            ActiveViewports = _viewportManager._activeViewports.Count,

            // Performance data
            SystemUpdateTimes = times,

            // System-specific debug info
            MetabolismEntities = 0, // TODO: _metabolismSystem.EntityCount,
            AiEntities = 0, // TODO: _aiManager.EntityCount,
            VegetationCount = 0 // TODO: _vegetationSystem.PlantCount
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