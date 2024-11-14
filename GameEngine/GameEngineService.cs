using WebPeli.GameEngine.EntitySystem.Interfaces;
using WebPeli.GameEngine.Managers;

namespace WebPeli.GameEngine;

public class GameEngineService : BackgroundService
{
    private readonly ILogger<GameEngineService> _logger;
    private readonly List<BaseManager> managers = [];
    private readonly List<BaseManager> systems = [];

    public GameEngineService(ILogger<GameEngineService> logger, ViewportManager viewportManager)
    {
        _logger = logger;
        // Build world
        var startTime = Environment.TickCount;
        World.GenerateWorld();
        _logger.LogInformation($"World generation took {Environment.TickCount - startTime}ms");
        _logger.LogInformation(World.GetWorldInfo());
        System.Console.WriteLine(World.GetWorldInfo());

        // Initialize managers
        managers.Add(new EntityRegister());
        managers.Add(new MapManager());
        // managers.Add(new ViewportManager());
        managers.Add(viewportManager);
        managers.Add(new MovementManager());
        managers.Add(new AiManager());


        InitManagers();

        systems.Add(new MetabolismSystem());

        InitSystems();

        // Add placeholder entities
        int num_entities = 1;
        for (int i = 0; i < num_entities; i++)
        {
            Guid entityId = Guid.NewGuid();
            managers[0].HandleMessage(new CreateEntity{EntityId = entityId, Capabilities = [EntityCapabilities.MetabolismSystem,
                                                                                            EntityCapabilities.MovementSystem,
                                                                                            EntityCapabilities.RenderingSystem,
                                                                                            EntityCapabilities.AiSystem]});

        }

    
    }

    private void InitManagers()
    {
        _logger.LogInformation("Initializing managers");
        foreach (BaseManager manager in managers)
        {
            manager.Init();
        }
    }

    private void DestroyManagers()
    {
        foreach (BaseManager manager in managers)
        {
            manager.Destroy();
        }
    }

    private void InitSystems()
    {
        _logger.LogInformation("Initializing systems");
        foreach (BaseManager system in systems)
        {
            system.Init();
        }
    }

    private void DestroySystems()
    {
        foreach (BaseManager system in systems)
        {
            system.Destroy();
        }
    }



    private int _lastUpdateTime = Environment.TickCount;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GameEngineService is starting.");
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

