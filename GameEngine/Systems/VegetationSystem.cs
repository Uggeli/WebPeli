using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World;
using WebPeli.GameEngine.World.WorldData;

namespace WebPeli.GameEngine.Systems;

public class VegetationSystem(ILogger<VegetationSystem> logger, PlantFSM plantFSM, Dictionary<Plant, PlantRequirements> plantTemplates) : BaseManager
{
    private readonly ILogger<VegetationSystem> _logger = logger;
    private readonly PlantFSM _plantFSM = plantFSM;
    private readonly Dictionary<Plant, PlantRequirements> _plantTemplates = plantTemplates;

    public override void Init()
    {
        EventManager.RegisterListener<SeedPlantedEvent>(this);
        EventManager.RegisterListener<SeasonChangeEvent>(this);
        EventManager.RegisterListener<TimeOfDayChangeEvent>(this);
        EventManager.RegisterListener<DamageEvent>(this);  // Plants can be damaged
        EventManager.RegisterListener<DeathEvent>(this);   // Plants can die
    }

    public override void Destroy()
    {
        EventManager.UnregisterListener<SeedPlantedEvent>(this);
        EventManager.UnregisterListener<SeasonChangeEvent>(this);
        EventManager.UnregisterListener<TimeOfDayChangeEvent>(this);
        EventManager.UnregisterListener<DamageEvent>(this);
        EventManager.UnregisterListener<DeathEvent>(this);
    }

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case SeedPlantedEvent seedEvent:
                HandleSeedPlanted(seedEvent);
                break;
            case SeasonChangeEvent seasonEvent:
                HandleSeasonChange(seasonEvent);
                break;
            case TimeOfDayChangeEvent timeEvent:
                HandleTimeOfDay(timeEvent);
                break;
            case DamageEvent damageEvent:
                HandleDamage(damageEvent);
                break;
            case DeathEvent deathEvent:
                HandleDeath(deathEvent);
                break;
        }
    }

    private void HandleSeedPlanted(SeedPlantedEvent evt)
    {
        var template = _plantTemplates[evt.Plant];
        var pos = evt.Position;

        // Verify suitable growing conditions
        var (material, surface, _) = WorldApi.GetTileInfo(pos);
        if (!template.SuitableGrowthTiles.Contains(material))
        {
            _logger.LogDebug("Seed can't grow on {Material} at {Position}", material, pos);
            return;
        }

        if (template.IsGroundCover)
        {
            // Ground cover just modifies tile surface, no entity needed
            var newSurface = evt.Plant switch
            {
                Plant.Grass => TileSurface.ShortGrass,
                Plant.Weed => TileSurface.TallGrass | TileSurface.Mud,  // Weeds make ground messy
                Plant.Flower => TileSurface.Flowers,  // Flowers grow in grass
                _ => surface
            };
            
            WorldApi.ModifyTile(pos, surface: newSurface);
            // No FSM tracking needed for surface plants
        }
        else
        {
            // Get positions first to check if we can even place the plant
            var positions = GetPlantPositions(evt.Plant, pos);
            
            // Verify all positions are valid before getting an ID
            if (!WorldApi.CanEntityFit(positions, volume: 200))  // Use constant or template value
            {
                _logger.LogDebug("Plant can't fit at {Position}", pos);
                return;
            }

            // Only get an ID if we know we can place the plant
            var entityId = IDManager.GetEntityId();

            // Register with EntityManager
            if (!WorldApi.AddEntity(entityId, positions))
            {
                _logger.LogError("Failed to add plant entity at {Position}", pos);
                IDManager.ReturnEntityId(entityId);  // Return the ID if we failed
                return;
            }

            _plantFSM.AddPlant(entityId, evt.Plant);
            
            // Add health component
            var health = PlantTemplates.CreateHealthComponent(template, PlantMaturityStatus.Seed);
            if (health.HasValue)
            {
                EventManager.Emit(new RegisterToSystem 
                { 
                    EntityId = entityId,
                    SystemType = SystemType.HealthSystem,
                    SystemData = health.Value
                });
            }
        }
    }

    private Position[] GetPlantPositions(Plant type, Position centerPos)
    {
        var template = _plantTemplates[type];
        var width = template.MaxWidth;
        
        if (width <= 1)
            return [centerPos];

        var positions = new List<Position>();
        var offset = width / 2;
        
        // Generate positions in a square around center
        for (int x = -offset; x <= offset; x++)
        {
            for (int y = -offset; y <= offset; y++)
            {
                positions.Add(centerPos + (x, y));
            }
        }
        
        return [.. positions];
    }

    private void HandleSeasonChange(SeasonChangeEvent evt)
    {
        _plantFSM.OnSeasonChanged(evt.NewSeason);
    }

    private void HandleTimeOfDay(TimeOfDayChangeEvent evt)
    {
        _plantFSM.Update(evt.NewTimeOfDay);
    }

    private void HandleDamage(DamageEvent evt)
    {
        // Wake up plant if it was damaged
        _plantFSM.WakePlant(evt.EntityId);
    }

    private void HandleDeath(DeathEvent evt)
    {
        // Just remove the plant from our tracking systems
        _plantFSM.RemovePlant(evt.EntityId);
    }

    public override void Update(double deltaTime)
    {
        // Most updates happen through event handlers
        // Regular update mainly for debugging/logging if needed
        var tick = Environment.TickCount;
        base.Update(deltaTime);
        _lastUpdateTime = Environment.TickCount - tick;
    }
}

// Extension to CreateEntity event to handle callback
public static class EntityRegisterExtensions
{
    public static void OnEntityCreated(this CreateEntity evt, int entityId)
    {
        // Handle the newly created entity
        if (evt.CallbackId.HasValue)
            EventManager.EmitCallback(evt.CallbackId.Value, entityId);
    }
}


public readonly record struct SeedPlantedEvent : IEvent
{
    public required Position Position { get; init; }
    public required Plant Plant { get; init; }
}

public enum Plant
{
    Tree,
    Bush,
    Flower,
    Grass,
    Weed,  // Long grass, nettles, etc
    None,
}

public enum PlantMaturityStatus
{
    Seed,  // Waits for suitable conditions
    Seedling,
    Sapling,
    Young,
    Mature,  // Reproducing
    None,
}

public readonly record struct PlantRequirements
{
    // Core properties
    public required bool IsGroundCover { get; init; }  // If true, it's a surface plant (grass, moss, etc)
    public required TileMaterial[] SuitableGrowthTiles { get; init; }
    
    // Size info (0 for ground cover)
    public required int MaxHeight { get; init; }
    public required int MaxWidth { get; init; }  // 1x1, 2x2, 3x3, 4x4
    
    // Growth requirements for advancing stages
    public required Dictionary<PlantMaturityStatus, GrowthRequirements> GrowthStages { get; init; }
    
    // What this plant yields when harvested/destroyed
    public required HarvestTable HarvestYields { get; init; }
}

public readonly record struct GrowthRequirements
{
    public required int MinDaysInStage { get; init; }  // Minimum days before can advance
    public required int MinTemperature { get; init; }
    public required int MaxTemperature { get; init; }
    public required int MinMoisture { get; init; }
    public required int MaxMoisture { get; init; }
    public required Season[] ValidSeasons { get; init; }  // When growth is possible
}
