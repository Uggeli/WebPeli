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
    private readonly CancellationTokenSource _shutdownTokenSource = new();

    public GameEngineService(ILogger<GameEngineService> logger,
                             ViewportManager viewportManager,
                             EntityRegister entityRegister,
                             MapManager mapManager,
                             AiManager aiManager,
                             MetabolismSystem metabolismSystem,
                             MovementSystem movementSystem,
                             TreeSystem treeSystem,
                             GroundCoverSystem groundCoverSystem,
                             TimeSystem timeSystem)
    {
        _logger = logger;
        // Build world
        var startTime = Environment.TickCount;
        _logger.LogInformation("Generating world... this might take a while");
        WorldApi.GenerateWorld();
        _logger.LogInformation($"World generation took {Environment.TickCount - startTime}ms");

        managers.Add(entityRegister);
        managers.Add(aiManager);
        managers.Add(mapManager);
        managers.Add(viewportManager);

        systems.Add(timeSystem);
        systems.Add(metabolismSystem);
        systems.Add(movementSystem);
        systems.Add(treeSystem);
        // systems.Add(groundCoverSystem);

        InitManagers();

        systems.Add(new MetabolismSystem());
        systems.Add(new MovementSystem());

        InitSystems();
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

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try 
        {
            _logger.LogInformation("GameEngineService is shutting down...");
            
            // Signal our own token to stop any running tasks
            _shutdownTokenSource.Cancel();

            // Destroy managers in reverse order of initialization
            foreach (var manager in managers.AsEnumerable().Reverse())
            {
                try 
                {
                    _logger.LogInformation("Shutting down manager: {ManagerType}", manager.GetType().Name);
                    manager.Destroy();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error shutting down manager: {ManagerType}", manager.GetType().Name);
                }
            }

            // Destroy systems in reverse order
            foreach (var system in systems.AsEnumerable().Reverse())
            {
                try 
                {
                    _logger.LogInformation("Shutting down system: {SystemType}", system.GetType().Name);
                    system.Destroy();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error shutting down system: {SystemType}", system.GetType().Name);
                }
            }

            await base.StopAsync(cancellationToken);
            _logger.LogInformation("GameEngineService shutdown complete.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during GameEngineService shutdown");
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GameEngineService is starting.");
        
        // Link the external token with our internal one
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken, _shutdownTokenSource.Token);

        try
        {
            await Task.Delay(1000, linkedTokenSource.Token);
            
            while (!linkedTokenSource.Token.IsCancellationRequested)
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
                await Task.Delay(Math.Max(Config.UpdateLoop - processingTime, 0), linkedTokenSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("GameEngineService execution cancelled.");
        }
    }

    public override void Dispose()
    {
        _shutdownTokenSource.Dispose();
        base.Dispose();
    }
}

