using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.Systems;
using WebPeli.GameEngine.World;

namespace WebPeli.GameEngine;

public class GameEngineService : BackgroundService
{
    private readonly ILogger<GameEngineService> _logger;
    private readonly List<BaseManager> managers = [];
    private readonly List<BaseManager> systems = [];

    public GameEngineService(ILogger<GameEngineService> logger, ViewportManager viewportManager, EntityRegister entityRegister)
    {
        _logger = logger;
        // Build world
        var startTime = Environment.TickCount;
        _logger.LogInformation("Generating world... this might take a while");
        WorldApi.GenerateWorld();
        _logger.LogInformation($"World generation took {Environment.TickCount - startTime}ms");

        // Initialize managers
        managers.Add(entityRegister);
        managers.Add(new MapManager());
        // managers.Add(new ViewportManager());
        managers.Add(viewportManager);
        managers.Add(new AiManager());


        InitManagers();

        systems.Add(new MetabolismSystem());
        systems.Add(new MovementSystem());

        InitSystems();

        // Add placeholder entities
        int num_entities = 1;
        for (int i = 0; i < num_entities; i++)
        {
            managers[0].HandleMessage(new CreateEntity{Capabilities = [EntityCapabilities.MetabolismSystem,
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
            await Task.Delay(Math.Max(Config.UpdateLoop - processingTime, 0), stoppingToken);
            
        }
    }
}

