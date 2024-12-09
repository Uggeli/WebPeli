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

public class VegetationSystem(ILogger<VegetationSystem> logger) : BaseManager
{
   
    

    private readonly ILogger<VegetationSystem> _logger = logger;
    public override void Init()
    {
        EventManager.RegisterListener<SeedPlantedEvent>(this);
        EventManager.RegisterListener<SeasonChangeEvent>(this);
        EventManager.RegisterListener<MoistureChangeEvent>(this);
        EventManager.RegisterListener<TemperatureChangeEvent>(this);
        EventManager.RegisterListener<SunlightChangeEvent>(this);
        EventManager.RegisterListener<WeatherChangeEvent>(this);
        EventManager.RegisterListener<TimeOfDayChangeEvent>(this);
    }
    public override void Destroy()
    {
        EventManager.UnregisterListener<SeedPlantedEvent>(this);
        EventManager.UnregisterListener<SeasonChangeEvent>(this);
        EventManager.UnregisterListener<MoistureChangeEvent>(this);
        EventManager.UnregisterListener<TemperatureChangeEvent>(this);
        EventManager.UnregisterListener<SunlightChangeEvent>(this);
        EventManager.UnregisterListener<WeatherChangeEvent>(this);
        EventManager.UnregisterListener<TimeOfDayChangeEvent>(this);
    }

    public override void HandleMessage(IEvent evt)
    {
        
    }


    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);
    }

    private void HandleSeasonChange(SeasonChangeEvent evt)
    {
        // Handle season change
    }

    private void HandleMoistureChange(MoistureChangeEvent evt)
    {
        // Handle moisture change
    }

    private void HandleTemperatureChange(TemperatureChangeEvent evt)
    {
        // Handle temperature change
    }

    private void HandleSunlightChange(SunlightChangeEvent evt)
    {
        // Handle sunlight change
    }

    private void HandleWeatherChange(WeatherChangeEvent evt)
    {
        // Handle weather change
    }

    private void HandleTimeOfDayChange(TimeOfDayChangeEvent evt)
    {
        // Handle time of day change
    }

    private void HandleSeedPlanted(SeedPlantedEvent evt)
    {
        // Handle seed planted
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
    Weed  // Long grass, nettles, etc
}

public struct PlantTemplate
{
    public Dictionary<PlantMaturityStatus, int> GrowthTime { get; init; }
    public TileMaterial[] SuitableTiles { get; init; }
    public int MaxHeight { get; init; }
    public int MaxWidth { get; init; } // 1x1, 2x2, 3x3, 4x4
    public bool IsGroundCover { get; init; } // if it is tile surface plant
    
}


public class PlantFSM
{

}

public enum PlantMaturityStatus
{
    Seed,  // Waits for suitable conditions
    Seedling,
    Sapling,
    Young,
    Mature,  // Reproducing
}
