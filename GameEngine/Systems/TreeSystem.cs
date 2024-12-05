using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World;
using WebPeli.GameEngine.World.WorldData;

namespace WebPeli.GameEngine.Systems;

// 

public class TreeSystem(ILogger<TreeSystem> logger) : BaseManager
{
    private readonly Dictionary<Position, TreeSpecies> _plantedSeeds = []; // Check seasonally
    private readonly Tree[] trees = []; // Idle trees
    private readonly Tree[] ActiveTrees = []; // Check seasonally
    

    private readonly ILogger<TreeSystem> _logger = logger;
    public override void Destroy()
    {
        
    }

    public override void HandleMessage(IEvent evt)
    {
        
    }

    public override void Init()
    {
        
    }

    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);
    }

    private void HandleSeedPlantedEvent(SeedPlantedEvent evt)
    {
        _plantedSeeds.Add(evt.position, evt.Species);
    }

    private void CheckIfSeedsCanGrow()
    {

    }

    


    private static class TreeTemplates
    {
        private static Dictionary<TreeSpecies, TreeTemplate> _treeTypes = new(){
            // Just placeholders for now
            {TreeSpecies.Pine, new TreeTemplate
            {
                Volume = 100,
                Surface = TileSurface.None,
                ValidMaterials = [TileMaterial.Dirt],
                MinMoisture = 25,
                OptimalMoisture = 50,
                MaxMoisture = 80,
                Age = 0,
                SeedlingThreshold = 200,   // Trees take longer
                MaturityThreshold = 1000,
                SeedRange = 15,             // Seeds can spread further
                GerminationChance = 15,    // But harder to grow
                IsContinuousSpreader = true,
                ReproductionThreshold = 500,
                TreeType = EntityType.Resource,  // Can be harvested
                MaxHeight = 5,
                GrowthRate = 1,
                OccupiedTiles = [new Position(0, 0)]  // Just one tile when planted
            }},
            {TreeSpecies.Oak, new TreeTemplate
            {
                Volume = 100,
                Surface = TileSurface.None,
                ValidMaterials = [TileMaterial.Dirt],
                MinMoisture = 25,
                OptimalMoisture = 50,
                MaxMoisture = 80,
                Age = 0,
                SeedlingThreshold = 200,   // Trees take longer
                MaturityThreshold = 1000,
                SeedRange = 15,             // Seeds can spread further
                GerminationChance = 15,    // But harder to grow
                IsContinuousSpreader = true,
                ReproductionThreshold = 500,
                TreeType = EntityType.Resource,  // Can be harvested
                MaxHeight = 5,
                GrowthRate = 1,
                OccupiedTiles = [new Position(0, 0)]  // Just one tile when planted
            }},
            {TreeSpecies.AppleTree, new TreeTemplate
            {
                Volume = 100,
                Surface = TileSurface.None,
                ValidMaterials = [TileMaterial.Dirt],
                MinMoisture = 25,
                OptimalMoisture = 50,
                MaxMoisture = 80,
                Age = 0,
                SeedlingThreshold = 200,   // Trees take longer
                MaturityThreshold = 1000,
                SeedRange = 15,             // Seeds can spread further
                GerminationChance = 15,    // But harder to grow
                IsContinuousSpreader = true,
                ReproductionThreshold = 500,
                TreeType = EntityType.Resource,  // Can be harvested
                MaxHeight = 5,
                GrowthRate = 1,
                OccupiedTiles = [new Position(0, 0)]  // Just one tile when planted
            }},
        };

        public static TreeTemplate GetTreeTemplate(TreeSpecies species)
        {
            return _treeTypes[species];
        }
    }
}

public record TreeTemplate : IBasePlant, IPlantReproduction
{
    public required byte Volume { get; init; }
    public required TileSurface Surface { get; init; }  // For shade effects maybe?
    public required TileMaterial[] ValidMaterials { get; init; }
    public required byte MinMoisture { get; init; }
    public required byte OptimalMoisture { get; init; }
    public required byte MaxMoisture { get; init; }
    public required int Age { get; init; }
    public required int SeedlingThreshold { get; init; }
    public required int MaturityThreshold { get; init; }
    public required byte SeedRange { get; init; }
    public required byte GerminationChance { get; init; }
    public required bool IsContinuousSpreader { get; init; }
    public required int ReproductionThreshold { get; init; }
    
    // Tree specific stuff
    public required EntityType TreeType { get; init; }
    public required int MaxHeight { get; init; }
    public required byte GrowthRate { get; init; }  // How fast it gains height
    public required Position[] OccupiedTiles { get; init; }  // For bigger trees
    // byte IPlantReproduction.ReproductionThreshold { get; init; }
}

public interface ITree {} // Marker interface for trees
public enum TreeSpecies : byte  // These would be mapped to templates
{
    Pine, // repro with seeds dust and wind
    Oak, // Drop leaves, repro with acorns
    AppleTree,  // Yield fruits, Drop leaves
}

public record struct Tree(Position position, TreeSpecies Species, int Age) : ITree;
public record struct SeedPlantedEvent(Position position, TreeSpecies Species) : IEvent;



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