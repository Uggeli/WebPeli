using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World;
using WebPeli.GameEngine.World.WorldData;

namespace WebPeli.GameEngine.Systems;

// Lifecycle of plants
// seed -> growing -> mature -> reproducing -> dying



// Statemachine
// Use Plantstatus enum to determine what the tree is doing and what it should do next or what it can do next

// Things we need to know about a tree
// - Age - Affects growth and yield
// - Health - Maybe as hitpoints to fell the tree, health system will handle this
// - Water -Moisture from mapmanager
// - Nutrients -We can asume that if tile material is good, the tree has nutrients
// - Sunlight
// - Temperature -Seasons can affect this
// - Height -Affect mainly on rendering, age * times species or something
// - Width - 4x4 tiles max
// - Leaves - how much sun is blocked
// - Branches - how much sun is blocked
// - Roots - Dont care
// - Fruits - Yield/loot
// - Seeds - Purely virtual, might be in solely in tree system
// - Flowers - Yield/loot
// - Bark -  Yield/loot, damage resistance? or do we care?
// - Sap - Yield/loot - Maybe
// - Resin - Yield/loot - Maybe
// - Wood - Yield/loot

// FSM

// States
// - Seed - Can only grow 
// - Seedling - Can only grow
// - Sapling - Can only grow

// Youth states can be stored together, we need just current state and age and tree type
// Update tree state based on age and tree type

// - Young - Grow, produce seeds, flowers, fruits
// - Mature - Grow, produce seeds, flowers, fruits, bark, sap, resin
// - Flowering state -> Fruiting state -> Harvesting state, Or drop fruits to reproduce
//   - Triggers - Age, season, health, sunlight, temperature, goes to cooldown state after

// - Reproducing state - Drop seeds
// - Cooldown state - Do nothing

// -Idle state - Do nothing, and remain so until neighbour tree wakes it up, or event triggers it

// Addional systems needed:
// - Weather system - Affects temperature, sunlight, water
// - Season system - Affects temperature, sunlight, water, Already implemented
// - Appearance system - Affects rendering



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

public readonly record struct PlantInstance
{
    // Core identifiers
    public required int EntityId { get; init; }
    public required Plant Type { get; init; }
    public required Position Position { get; init; }
    
    // Current state
    public required PlantMaturityStatus Status { get; init; }
    public required int Age { get; init; }  // In days
    public required int DaysInCurrentStage { get; init; }
}

public static class PlantTemplates 
{
    // Example template for grass (ground cover)
    public static readonly PlantRequirements Grass = new()
    {
        IsGroundCover = true,
        SuitableGrowthTiles = [TileMaterial.Dirt, TileMaterial.Mud],
        MaxHeight = 0,  // Ground cover
        MaxWidth = 0,   // Ground cover
        GrowthStages = new()
        {
            { PlantMaturityStatus.Seed, new GrowthRequirements 
                {
                    MinDaysInStage = 2,
                    MinTemperature = 5,
                    MaxTemperature = 35,
                    MinMoisture = 20,
                    MaxMoisture = 80,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn]
                }
            },
            { PlantMaturityStatus.Mature, new GrowthRequirements 
                {
                    MinDaysInStage = 0,  // No limit
                    MinTemperature = 0,
                    MaxTemperature = 40,
                    MinMoisture = 10,
                    MaxMoisture = 90,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn]
                }
            }
        },
        HarvestYields = new HarvestTable() // Define grass yields
    };

    public static readonly PlantRequirements Weed = new()
    {
        IsGroundCover = true,
        SuitableGrowthTiles = [TileMaterial.Dirt, TileMaterial.Mud],
        MaxHeight = 0,  // Ground cover
        MaxWidth = 0,   // Ground cover
        GrowthStages = new()
        {
            { PlantMaturityStatus.Seed, new GrowthRequirements 
                {
                    MinDaysInStage = 2,
                    MinTemperature = 5,
                    MaxTemperature = 35,
                    MinMoisture = 20,
                    MaxMoisture = 80,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn]
                }
            },
            { PlantMaturityStatus.Mature, new GrowthRequirements 
                {
                    MinDaysInStage = 0,  // No limit
                    MinTemperature = 0,
                    MaxTemperature = 40,
                    MinMoisture = 10,
                    MaxMoisture = 90,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn]
                }
            }
        },
        HarvestYields = new HarvestTable() // Define grass yields
    };

    public static readonly PlantRequirements Flower = new()
    {
        IsGroundCover = true,
        SuitableGrowthTiles = [TileMaterial.Dirt, TileMaterial.Mud],
        MaxHeight = 0,  // Ground cover
        MaxWidth = 0,   // Ground cover
        GrowthStages = new()
        {
            { PlantMaturityStatus.Seed, new GrowthRequirements 
                {
                    MinDaysInStage = 2,
                    MinTemperature = 5,
                    MaxTemperature = 35,
                    MinMoisture = 20,
                    MaxMoisture = 80,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn]
                }
            },
            { PlantMaturityStatus.Mature, new GrowthRequirements 
                {
                    MinDaysInStage = 0,  // No limit
                    MinTemperature = 0,
                    MaxTemperature = 40,
                    MinMoisture = 10,
                    MaxMoisture = 90,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn]
                }
            }
        },
        HarvestYields = new HarvestTable() // Define grass yields
    };
    // Example template for an oak tree (physical plant)
    public static readonly PlantRequirements OakTree = new()
    {
        IsGroundCover = false,
        SuitableGrowthTiles = [TileMaterial.Dirt, TileMaterial.Mud],
        MaxHeight = 5,  // Will affect rendering
        MaxWidth = 2,   // 2x2 tiles at full size
        GrowthStages = new()
        {
            { PlantMaturityStatus.Seed, new GrowthRequirements 
                {
                    MinDaysInStage = 5,
                    MinTemperature = 5,
                    MaxTemperature = 35,
                    MinMoisture = 30,
                    MaxMoisture = 80,
                    ValidSeasons = [Season.Spring]
                }
            },
            { PlantMaturityStatus.Seedling, new GrowthRequirements 
                {
                    MinDaysInStage = 15,
                    MinTemperature = 0,
                    MaxTemperature = 35,
                    MinMoisture = 20,
                    MaxMoisture = 80,
                    ValidSeasons = [Season.Spring, Season.Summer]
                }
            },
            { PlantMaturityStatus.Sapling, new GrowthRequirements 
                {
                    MinDaysInStage = 30,
                    MinTemperature = -5,
                    MaxTemperature = 40,
                    MinMoisture = 20,
                    MaxMoisture = 70,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn]
                }
            },
            { PlantMaturityStatus.Young, new GrowthRequirements 
                {
                    MinDaysInStage = 60,
                    MinTemperature = -10,
                    MaxTemperature = 40,
                    MinMoisture = 20,
                    MaxMoisture = 70,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn]
                }
            },
            { PlantMaturityStatus.Mature, new GrowthRequirements 
                {
                    MinDaysInStage = 0,  // No limit
                    MinTemperature = -20,
                    MaxTemperature = 45,
                    MinMoisture = 20,
                    MaxMoisture = 70,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn, Season.Winter]
                }
            }
        },
        HarvestYields = new HarvestTable() // Define oak tree yields
    };

    // Method to create health component only for physical plants
    public static HealthComponent? CreateHealthComponent(PlantRequirements template, PlantMaturityStatus status)
    {
        if (template.IsGroundCover)
            return null;

        var baseHealth = template.MaxWidth * template.MaxWidth * 50; // Health based on size
        
        var healthMultiplier = status switch
        {
            PlantMaturityStatus.Seed => 0.2f,
            PlantMaturityStatus.Seedling => 0.4f,
            PlantMaturityStatus.Sapling => 0.6f,
            PlantMaturityStatus.Young => 0.8f,
            PlantMaturityStatus.Mature => 1.0f,
            _ => throw new ArgumentException($"Unknown status: {status}")
        };

        return new HealthComponent
        {
            Health = (int)(baseHealth * healthMultiplier),
            MaxHealth = (int)(baseHealth * healthMultiplier),
            RegenRate = template.MaxWidth // Bigger plants regenerate faster
        };
    }
}
