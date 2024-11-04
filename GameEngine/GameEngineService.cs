using WebPeli.GameEngine.Managers;

namespace WebPeli.GameEngine;

public class GameEngineService : BackgroundService
{
    private readonly ILogger<GameEngineService> _logger;
    private readonly List<BaseManager> managers = [];

    public GameEngineService(ILogger<GameEngineService> logger, MapManager mapManager, ViewportManager viewportManager)
    {
        _logger = logger;
        managers.Add(mapManager);
        managers.Add(viewportManager);

        InitManagers();
    }

    private void InitManagers()
    {
        foreach (BaseManager manager in managers)
        {
            manager.Init();
        }
    }

    private int _lastUpdateTime = Environment.TickCount;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var startTick = Environment.TickCount;
            float deltaTime = (startTick - _lastUpdateTime) / 1000f;
            _lastUpdateTime = startTick;

            foreach (BaseManager manager in managers)
            {
                manager.Update(deltaTime);
            }

            var processingTime = Environment.TickCount - startTick;
            await Task.Delay(Math.Max(16 - processingTime, 0), stoppingToken);
        }
    }
}

