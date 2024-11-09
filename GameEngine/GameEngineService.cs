using WebPeli.GameEngine.EntitySystem.Interfaces;
using WebPeli.GameEngine.Managers;

namespace WebPeli.GameEngine;

public class GameEngineService : BackgroundService
{
    private readonly ILogger<GameEngineService> _logger;
    private readonly List<BaseManager> managers = [];
    private readonly List<BaseManager> systems = [];

    public GameEngineService(ILogger<GameEngineService> logger)
    {
        _logger = logger;

        managers.Add(new EntityRegister());
        managers.Add(new MapManager());
        managers.Add(new ViewportManager());
        managers.Add(new MovementManager());
        managers.Add(new AiManager());


        InitManagers();

        systems.Add(new MetabolismSystem());

        // Add placeholder entities
        for (int i = 0; i < 10; i++)
        {
            Guid entityId = Guid.NewGuid();
            managers[0].HandleMessage(new CreateEntity(entityId, EntityCapabilities.Metabolism));
            managers[0].HandleMessage(new CreateEntity(entityId, EntityCapabilities.Movement));
            managers[0].HandleMessage(new CreateEntity(entityId, EntityCapabilities.Render));
            managers[0].HandleMessage(new CreateEntity(entityId, EntityCapabilities.AiSystem));
        }

    
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

            foreach (BaseManager system in systems)
            {
                system.Update(deltaTime);
            }

            foreach (BaseManager manager in managers)
            {
                manager.Update(deltaTime);
            }


            var processingTime = Environment.TickCount - startTick;
            await Task.Delay(Math.Max(16 - processingTime, 0), stoppingToken);
        }
    }
}

