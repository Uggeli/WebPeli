using System.Net.WebSockets;
using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Systems;
using WebPeli.Logging;
using WebPeli.Network;

namespace WebPeli.GameEngine;

public class DebugDataService(
    ILogger<DebugDataService> logger,
    ViewportManager viewportManager,
    EntityRegister entityRegister,
    MovementSystem movementSystem,
    TimeSystem timeSystem,
    MetabolismSystem metabolismSystem,
    VegetationSystem vegetationSystem,
    AiManager aiManager,
    HarvestSystem harvestSystem,
    HealthSystem healthSystem,
    MessageCapturingProvider messageCapturingProvider)
{
    private readonly MessageCapturingProvider _messageCapturingProvider = messageCapturingProvider;
    private readonly Dictionary<string, int> _systemUpdateTimes = new();
    private readonly HashSet<WebSocket> _debugSockets = new();
    private readonly object _updateLock = new();
    
    // System references
    private readonly ILogger<DebugDataService> _logger = logger;
    private readonly ViewportManager _viewportManager = viewportManager;
    private readonly EntityRegister _entityRegister = entityRegister;
    private readonly MovementSystem _movementSystem = movementSystem;
    private readonly TimeSystem _timeSystem = timeSystem;
    private readonly MetabolismSystem _metabolismSystem = metabolismSystem;
    private readonly VegetationSystem _vegetationSystem = vegetationSystem;
    private readonly AiManager _aiManager = aiManager;
    private readonly HarvestSystem _harvestSystem = harvestSystem;
    private readonly HealthSystem _healthSystem = healthSystem;
    private bool _isRunning = false;

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

    public void HandleLogMessageRequest(WebSocket socket, string category = "WebPeli")
    {
        var messages = _messageCapturingProvider.GetMessagesForCategory(category);
        var data = MessageProtocol.EncodeLogMessages(messages);
        socket.SendAsync(
            new ArraySegment<byte>(data), 
            WebSocketMessageType.Binary, 
            true, 
            CancellationToken.None);
    }

    public IEnumerable<LogMessage> GetMessagesForCategory(string category = "WebPeli")
    {
        return _messageCapturingProvider.GetMessagesForCategory(category);
    }

    private DebugState CollectDebugState()
    {
        var newLogs = _messageCapturingProvider.GetNewMessages();

        Dictionary<string, int> times = new() {
            // Managers
            { "ViewportManager", _viewportManager.UpdateTime },
            { "AiManager", _aiManager.UpdateTime },
            { "EntityRegister", _entityRegister.UpdateTime },
            // Systems
            { "MovementSystem", _movementSystem.UpdateTime },
            { "TimeSystem", _timeSystem.UpdateTime },
            { "MetabolismSystem", _metabolismSystem.UpdateTime },
            { "VegetationSystem", _vegetationSystem.UpdateTime },
            { "HarvestSystem", _harvestSystem.UpdateTime },
            { "HealthSystem", _healthSystem.UpdateTime }
        };


        return new DebugState
        {
            // Time system data
            Season = TimeSystem.CurrentSeason.ToString(),
            TimeOfDay = TimeSystem.CurrentTimeOfDay.ToString(),
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
            VegetationCount = 0, // TODO: _vegetationSystem.PlantCount
            NewLogMessages = newLogs.ToArray()
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
                            await socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, cancellationToken);
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