using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World;
using WebPeli.GameEngine.World.WorldData;

namespace WebPeli.GameEngine.Systems;

public class TreeSystem(ILogger<TreeSystem> logger) : BaseManager
{
    private readonly Dictionary<int, (TreeTemplate Template, PlantStatus Status)> _trees = [];
    private readonly Dictionary<int, HashSet<Position>> _currentTiles = [];  // Track current occupied tiles
    private readonly Dictionary<int, int> _ages = [];
    private readonly ILogger<TreeSystem> _logger = logger;
    private static readonly Random _rng = new();

    public override void Init()
    {
        EventManager.RegisterListener<PlantReproductionEvent>(this);
        EventManager.RegisterListener<MoistureChangeEvent>(this);
        
        // Maybe spawn some initial trees
        InitializeStarterTrees();
    }

    private void InitializeStarterTrees()
    {
        // Space out initial trees
        for (int x = 10; x < Config.WORLD_TILES; x += 20)
        {
            for (int y = 10; y < Config.WORLD_TILES; y += 20)
            {
                TryPlantTree(new Position(x, y), TreeTemplates.BasicTree);
            }
        }
    }

    private void TryPlantTree(Position pos, TreeTemplate template)
    {
        var callbackId = EventManager.RegisterCallback((byte moisture) =>
        {
            var (material, surface, _) = WorldApi.GetTileInfo(pos);
            
            if (!template.ValidMaterials.Contains(material) ||
                moisture < template.MinMoisture ||
                moisture > template.MaxMoisture ||
                _rng.Next(255) >= template.GerminationChance)
            {
                return;
            }

            // Create entity with initial single tile
            var entityId = IDManager.GetEntityId();
            var initialPos = template.OccupiedTiles[0] + pos;  // Translate to world pos

            // Check if we can place the tree
            if (!WorldApi.CanEntityFit([initialPos], template.Volume))
            {
                IDManager.ReturnEntityId(entityId);
                return;
            }

            // Create tree entity
            if (WorldApi.AddEntity(entityId, [initialPos], template.Volume))
            {
                WorldApi.SetEntityType(entityId, template.TreeType);
                _trees[entityId] = (template, PlantStatus.Seed | PlantStatus.Growing);
                _ages[entityId] = 0;
                _currentTiles[entityId] = [initialPos];
                
                _logger.LogInformation($"New tree {entityId} planted at {pos}");
            }
            else
            {
                IDManager.ReturnEntityId(entityId);
            }
        });

        EventManager.Emit(new MoistureRequest { Position = pos, CallbackId = callbackId });
    }

    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);  // Handle queued events

        foreach (var (entityId, (plant, status)) in _trees.ToArray())
        {
            if (status.HasFlag(PlantStatus.Dead))
                continue;

            _ages[entityId]++;
            
            // Update growth status and size
            UpdateTreeGrowth(entityId);
            
            // Check reproduction for mature trees
            if (status.HasFlag(PlantStatus.Mature) &&
                _trees[entityId].Template is IPlantReproduction &&
                _ages[entityId] >= plant.ReproductionThreshold)
            {
                TryReproduction(entityId);
            }

            // Periodic moisture check
            if (_ages[entityId] % Config.MOISTURE_CHECK_INTERVAL == 0)
            {
                CheckTreeMoisture(entityId);
            }
        }
    }

    private void UpdateTreeGrowth(int entityId)
    {
        if (!_trees.TryGetValue(entityId, out var treeData))
            return;

        var (template, currentStatus) = treeData;
        var age = _ages[entityId];
        var newStatus = currentStatus;

        // Calculate how many tiles it should occupy at current age
        int targetTileCount = Math.Min(
            template.OccupiedTiles.Length,
            1 + (age / template.GrowthRate)
        );

        // If we need to grow
        if (targetTileCount > _currentTiles[entityId].Count)
        {
            GrowTree(entityId, targetTileCount);
        }

        // Update status based on age
        if (age >= template.MaturityThreshold && !currentStatus.HasFlag(PlantStatus.Mature))
        {
            newStatus = PlantStatus.Mature;
        }
        else if (age >= template.SeedlingThreshold && !currentStatus.HasFlag(PlantStatus.Seedling))
        {
            newStatus = PlantStatus.Seedling | PlantStatus.Growing;
        }

        if (newStatus != currentStatus)
        {
            _trees[entityId] = (template, newStatus);
            EventManager.Emit(new PlantStatusChanged 
            { 
                Position = _currentTiles[entityId].First(), // Use root position
                OldStatus = currentStatus,
                NewStatus = newStatus
            });
        }
    }

    private void GrowTree(int entityId, int targetTileCount)
    {
        var (template, _) = _trees[entityId];
        var currentPos = _currentTiles[entityId].First();  // Root position
        var newPositions = template.OccupiedTiles
            .Take(targetTileCount)
            .Select(offset => currentPos + offset)
            .ToArray();

        // Check if we can expand
        if (WorldApi.CanEntityFit(newPositions, template.Volume))
        {
            if (WorldApi.TryMoveEntity(entityId, newPositions))
            {
                _currentTiles[entityId] = [.. newPositions];
                _logger.LogDebug($"Tree {entityId} grew to {targetTileCount} tiles");
            }
        }
    }

    private void CheckTreeMoisture(int entityId)
    {
        // Check moisture at root position
        var rootPos = _currentTiles[entityId].First();
        
        var callbackId = EventManager.RegisterCallback((byte moisture) =>
        {
            if (!_trees.TryGetValue(entityId, out var treeData))
                return;

            var (template, status) = treeData;
            
            if (moisture < template.MinMoisture || moisture > template.MaxMoisture)
            {
                if (status.HasFlag(PlantStatus.Dying))
                {
                    KillTree(entityId);
                }
                else
                {
                    _trees[entityId] = (template, status | PlantStatus.Dying);
                }
            }
            else if (status.HasFlag(PlantStatus.Dying))
            {
                _trees[entityId] = (template, status & ~PlantStatus.Dying);
            }
        });

        EventManager.Emit(new MoistureRequest { Position = rootPos, CallbackId = callbackId });
    }

    private void UpdateTreeMoisture(Position pos)
    {

    }

    private void KillTree(int entityId)
    {
        WorldApi.RemoveEntity(entityId);
        _trees.Remove(entityId);
        _currentTiles.Remove(entityId);
        _ages.Remove(entityId);
        _logger.LogDebug($"Tree {entityId} died");
    }

    private void TryReproduction(int entityId)
    {
        if (!_trees.TryGetValue(entityId, out var treeData))
            return;

        var (template, _) = treeData;
        var rootPos = _currentTiles[entityId].First();
        var seedPositions = GetSeedPositions(rootPos, template.SeedRange);
        
        foreach (var seedPos in seedPositions)
        {
            if (!WorldApi.IsInWorldBounds(seedPos))
                continue;

            if (_rng.Next(100) < template.GerminationChance)
            {
                TryPlantTree(seedPos, template);
            }
        }
    }

    public override void Destroy()
    {
        EventManager.UnregisterListener<PlantReproductionEvent>(this);
        EventManager.UnregisterListener<MoistureChangeEvent>(this);
    }

    // Helpers
    private static Position[] GetSeedPositions(Position origin, byte range)
    {
        var positions = new List<Position>();
        for(int x = -range; x <= range; x++)
        {
            for(int y = -range; y <= range; y++)
            {
                if(Math.Abs(x) + Math.Abs(y) <= range)
                {
                    positions.Add(origin + (x, y));
                }
            }
        }
        return positions.ToArray();
    }

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case PlantReproductionEvent reproductionEvent:
                TryReproduction(reproductionEvent.EntityId);
                break;
            case MoistureChangeEvent moistureEvent:
                UpdateTreeMoisture(moistureEvent.Position);
                break;
        }
    }
}