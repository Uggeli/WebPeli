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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // _logger.LogInformation("GameEngineService running at: {time}", DateTimeOffset.Now);
            Update();
            await Task.Delay(1000, stoppingToken);  // Later: reduce delay to 16ms for 60 FPS
        }
    }

    private void Update()
    {
        foreach (BaseManager manager in managers)
        {
            manager.Update();
        }
    }
}

