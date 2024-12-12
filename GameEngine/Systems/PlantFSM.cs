using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World;

namespace WebPeli.GameEngine.Systems;
public class PlantFSM(Dictionary<Plant, PlantRequirements> templates, ILogger<PlantFSM> logger)
{
    private readonly ILogger<PlantFSM> _logger = logger;
    private const int MAX_PLANTS = Config.MAX_TREES;

    // ID mapping
    private readonly int[] _entityIds = new int[MAX_PLANTS];  // Maps array index -> entity ID
    private readonly Dictionary<int, int> _indexMap = [];  // Maps entity ID -> array index
    private int _nextIndex = 0;

    // Core plant data arrays - direct indexed access
    private readonly Plant[] _types = new Plant[MAX_PLANTS];
    private readonly PlantMaturityStatus[] _status = new PlantMaturityStatus[MAX_PLANTS];
    private readonly int[] _age = new int[MAX_PLANTS];
    private readonly int[] _daysInStage = new int[MAX_PLANTS];
    private readonly bool[] _active = new bool[MAX_PLANTS];

    // Track active entities for efficient iteration
    private readonly HashSet<int> _activeIndices = new(MAX_PLANTS);
    private readonly Dictionary<Plant, PlantRequirements> _templates = templates;

    public void OnSeasonChanged(Season newSeason)
    {
        _logger.LogInformation("Season changed to {Season}", newSeason);
        var plantsToWake = new List<int>();
        var plantsToSleep = new List<int>();

        for (int index = 0; index < MAX_PLANTS; index++)
        {
            if (_entityIds[index] == 0) continue;  // Skip empty slots

            var plant = _types[index];
            var isActive = _active[index];
            var status = _status[index];

            if (!isActive)
            {
                if (_templates[plant].GrowthStages[PlantMaturityStatus.Mature].ValidSeasons.Contains(newSeason))
                {
                    plantsToWake.Add(index);
                }
            }
            else 
            {
                if (!_templates[plant].GrowthStages[status].ValidSeasons.Contains(newSeason))
                {
                    plantsToSleep.Add(index);
                }
            }
        }
        _logger.LogInformation("Waking {Count} plants and putting {Count} to sleep", plantsToWake.Count, plantsToSleep.Count);
        
        foreach (var index in plantsToWake)
        {
            _active[index] = true;
            _activeIndices.Add(index);
        }

        foreach (var index in plantsToSleep)
        {
            _active[index] = false;
            _activeIndices.Remove(index);
        }
    }

    public void Update(TimeOfDay timeOfDay)
    {
        if (timeOfDay != TimeOfDay.Dawn) return;
        _logger.LogInformation("Updating plants at {TimeOfDay}", timeOfDay);
        _logger.LogInformation("Active plants: {Count}", _activeIndices.Count);

        foreach (var index in _activeIndices)
        {
            UpdatePlant(index);
        }
    }

    private void UpdatePlant(int index)
    {
        
        var plant = _types[index];
        var template = _templates[plant];
        var entityId = _entityIds[index];

        if (template.IsGroundCover) return;

        _age[index]++;
        _daysInStage[index]++;

        if (_status[index] == PlantMaturityStatus.Mature)
        {
            var currentSeason = TimeSystem.CurrentSeason;
            if (ShouldSpreadSeeds(plant, currentSeason))
            {
                _logger.LogInformation("Plant at index {Index} is spreading seeds", index);
                SpreadSeeds(entityId, plant);
            }
        }
        else if (CanAdvanceStage(index, template))
        {
            var nextStatus = GetNextStatus(_status[index]);
            if (nextStatus != _status[index])
            {
                _status[index] = nextStatus;
                _daysInStage[index] = 0;
            }
        }
    }

    private static bool ShouldSpreadSeeds(Plant type, Season season) =>
        (type, season) switch
        {
            (Plant.Tree, Season.Autumn) => Tools.Random.Next(100) < 5,
            (Plant.Bush, Season.Autumn) => Tools.Random.Next(100) < 3,
            (Plant.Flower, Season.Summer) => Tools.Random.Next(100) < 10,
            (Plant.Grass, _) => Tools.Random.Next(100) < 15,
            (Plant.Weed, _) => Tools.Random.Next(100) < 20,
            _ => false
        };

    private void SpreadSeeds(int entityId, Plant type)
    {
        _logger.LogInformation("Spreading seeds for plant {Plant} with entity ID {EntityId}", type, entityId);
        var parentPositions = WorldApi.GetEntityPositions(entityId);
        if (parentPositions.Length == 0) return;
        
        var parentPos = parentPositions[0];
        var radius = type switch
        {
            Plant.Tree => 3,
            Plant.Bush => 2,
            _ => 1
        };

        var angle = Tools.Random.NextDouble() * Math.PI * 2;
        var distance = Tools.Random.Next(1, radius + 1);
        var offsetX = (int)(Math.Cos(angle) * distance);
        var offsetY = (int)(Math.Sin(angle) * distance);
        var newPos = parentPos + (offsetX, offsetY);

        if (!WorldApi.IsInWorldBounds(newPos)) return;

        EventManager.Emit(new SeedPlantedEvent
        {
            Position = newPos,
            Plant = type
        });
        _logger.LogInformation("Seed planted at position {Position}", newPos);
    }

    private bool CanAdvanceStage(int index, PlantRequirements template)
    {
        var currentReqs = template.GrowthStages[_status[index]];
        
        if (_daysInStage[index] < currentReqs.MinDaysInStage)
            return false;

        // Get environmental conditions for the entity's position
        var positions = WorldApi.GetEntityPositions(_entityIds[index]);
        if (positions.Length == 0) return false;

        var pos = positions[0];
        var tile = WorldApi.GetTileInfo(pos);
        
        // TODO: Add environment checks from template requirements

        return true;
    }

    private static PlantMaturityStatus GetNextStatus(PlantMaturityStatus current) => current switch
    {
        PlantMaturityStatus.Seed => PlantMaturityStatus.Seedling,
        PlantMaturityStatus.Seedling => PlantMaturityStatus.Sapling,
        PlantMaturityStatus.Sapling => PlantMaturityStatus.Young,
        PlantMaturityStatus.Young => PlantMaturityStatus.Mature,
        _ => current
    };

    public void AddPlant(int entityId, Plant type)
    {
        _logger.LogInformation("Adding plant {Plant} with entity ID {EntityId}", type, entityId);
        while (_nextIndex < MAX_PLANTS && _entityIds[_nextIndex] != 0)
        {
            _nextIndex++;
        }

        if (_nextIndex >= MAX_PLANTS)
        {
            _logger.LogError("Cannot add plant - no free slots available");
            return;
        }

        var index = _nextIndex;
        _entityIds[index] = entityId;
        _indexMap[entityId] = index;

        _types[index] = type;
        _status[index] = PlantMaturityStatus.Seed;
        _age[index] = 0;
        _daysInStage[index] = 0;

        var currentSeason = TimeSystem.CurrentSeason;
        if (_templates[type].GrowthStages[PlantMaturityStatus.Seed].ValidSeasons.Contains(currentSeason))
        {
            _logger.LogInformation("Activating plant at index {Index}", index);
            _active[index] = true;
            _activeIndices.Add(index);
        }
    }

    public void RemovePlant(int entityId)
    {
        _logger.LogInformation("Removing plant with entity ID {EntityId}", entityId);
        if (!_indexMap.TryGetValue(entityId, out int index))
        {
            return;
        }

        _types[index] = Plant.None;
        _status[index] = PlantMaturityStatus.None;
        _age[index] = 0;
        _daysInStage[index] = 0;
        _active[index] = false;
        _activeIndices.Remove(index);

        _entityIds[index] = 0;
        _indexMap.Remove(entityId);

        if (index < _nextIndex)
        {
            _nextIndex = index;
        }
    }

    public void WakePlant(int entityId)
    {
        if (!_indexMap.TryGetValue(entityId, out int index))
        {
            return;
        }

        var plant = _types[index];
        var currentSeason = TimeSystem.CurrentSeason;
            
        if (_templates[plant].GrowthStages[PlantMaturityStatus.Mature].ValidSeasons.Contains(currentSeason))
        {
            _active[index] = true;
            _activeIndices.Add(index);
            _status[index] = PlantMaturityStatus.Mature;
            _daysInStage[index] = 0;
        }
    }
}
