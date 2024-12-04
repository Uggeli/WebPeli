using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World;
using WebPeli.GameEngine.World.WorldData;

namespace WebPeli.GameEngine.Systems;

public class GroundCoverSystem(ILogger<GroundCoverSystem> logger) : BaseManager
{
    private readonly Dictionary<Position, (IBasePlant Plant, PlantStatus Status)> _activePlants = [];
    private readonly byte[] _ages = new byte[Config.WORLD_TILES * Config.WORLD_TILES];
    private readonly ILogger<GroundCoverSystem > _logger = logger;

    public override void Init()
    {
        EventManager.RegisterListener<PlantReproductionEvent>(this);
        EventManager.RegisterListener<MoistureChangeEvent>(this);
        // Initial seeding if we want some starter grass
        InitializeStarterGroundCover();
        _logger.LogInformation("Ground cover system initialized");
    }

    private void InitializeStarterGroundCover()
    {
        // Maybe seed some initial patches in good spots
        for (int x = 0; x < Config.WORLD_TILES; x += 10)
        {
            for (int y = 0; y < Config.WORLD_TILES; y += 10)
            {
                var pos = new Position(x, y);
                TryPlantGroundCover(pos, PlantTemplates.ShortGrass);
            }
        }
    }

    private void TryPlantGroundCover(Position pos, GroundCoverPlant plant)
    {
        var callbackId = EventManager.RegisterCallback((byte moisture) =>
        {
            var (material, surface, _) = WorldApi.GetTileInfo(pos);
            
            if (!plant.ValidMaterials.Contains(material) ||
                surface != TileSurface.None ||
                moisture < plant.MinMoisture ||
                moisture > plant.MaxMoisture ||
                Tools.Random.Next(255) >= plant.GerminationChance)
            {
                return;
            }

            // Good to grow!
            if (WorldApi.ModifyTile(pos, surface: plant.Surface))
            {
                _activePlants[pos] = (plant, PlantStatus.Seed | PlantStatus.Growing);
                _ages[pos.WorldToIndex()] = 0;
                
                _logger.LogDebug($"New {plant.Surface} planted at {pos}");
            }
        });

        EventManager.Emit(new MoistureRequest { Position = pos, CallbackId = callbackId });
    }

    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);
        foreach (var (pos, (plant, status)) in _activePlants.ToArray())
        {
            if (status.HasFlag(PlantStatus.Dead)) continue;

            var idx = pos.WorldToIndex();
            _ages[idx]++;
            
            // Update growth status
            UpdatePlantStatus(pos);
            
            // Check if it's time to try reproduction
            if (status.HasFlag(PlantStatus.Mature) && 
                plant is IPlantReproduction repro &&
                _ages[idx] >= repro.ReproductionThreshold)
            {
                TryReproduction(pos, repro);
            }

            // Periodic moisture check for survival
            if (_ages[idx] % Config.MOISTURE_CHECK_INTERVAL == 0)
            {
                CheckMoisture(pos);
            }
        }
    }

    private void UpdatePlantStatus(Position pos)
    {
        if (!_activePlants.TryGetValue(pos, out var plantData))
            return;

        var (plant, currentStatus) = plantData;
        var age = _ages[pos.WorldToIndex()];
        var newStatus = currentStatus;

        // Progress through growth stages
        if (age >= plant.MaturityThreshold && !currentStatus.HasFlag(PlantStatus.Mature))
        {
            if (currentStatus.HasFlag(PlantStatus.Mature))
            {
                return;
            }
            newStatus = PlantStatus.Mature;
            _logger.LogDebug($"Plant matured at {pos}");
            
            // Short grass can grow into tall grass if conditions are good
            // if (plant.Surface == TileSurface.ShortGrass)
            // {
            //     CheckForTallGrassUpgrade(pos);
            // }
        }
        else if (age >= plant.SeedlingThreshold && !currentStatus.HasFlag(PlantStatus.Seedling))
        {
            newStatus = PlantStatus.Seedling | PlantStatus.Growing;
        }

        if (newStatus != currentStatus)
        {
            _activePlants[pos] = (plant, newStatus);
            EventManager.Emit(new PlantStatusChanged 
            { 
                Position = pos,
                OldStatus = currentStatus,
                NewStatus = newStatus
            });
        }
    }

    private void CheckForTallGrassUpgrade(Position pos)
    {
        var callbackId = EventManager.RegisterCallback((byte moisture) =>
        {
            // Only upgrade if moisture is in tall grass range
            if (moisture >= PlantTemplates.TallGrass.MinMoisture && 
                moisture <= PlantTemplates.TallGrass.MaxMoisture &&
                Tools.Random.Next(100) < 20)  // 20% chance to upgrade
            {
                WorldApi.ModifyTile(pos, surface: TileSurface.TallGrass);
                _activePlants[pos] = (PlantTemplates.TallGrass, PlantStatus.Mature);
                _logger.LogDebug($"Short grass upgraded to tall grass at {pos}");
            }
        });

        EventManager.Emit(new MoistureRequest { Position = pos, CallbackId = callbackId });
    }

    private void TryReproduction(Position pos, IPlantReproduction plant)
    {
        var seedPositions = GetSeedPositions(pos, plant.SeedRange);
 
        foreach (var seedPos in seedPositions)
        {
            if (!WorldApi.IsInWorldBounds(seedPos))
                continue;

            if (Tools.Random.Next(100) < plant.GerminationChance && plant is GroundCoverPlant groundCoverPlant)
            {
                TryPlantGroundCover(seedPos, groundCoverPlant);
            }
        }
    }

    private void CheckMoisture(Position pos)
    {
        var callbackId = EventManager.RegisterCallback((byte moisture) =>
        {
            if (!_activePlants.TryGetValue(pos, out var plantData))
                return;

            var (plant, status) = plantData;
            
            if (moisture < plant.MinMoisture || moisture > plant.MaxMoisture)
            {
                if (status.HasFlag(PlantStatus.Dying))
                {
                    KillPlant(pos);
                }
                else
                {
                    _activePlants[pos] = (plant, status | PlantStatus.Dying);
                }
            }
            else if (status.HasFlag(PlantStatus.Dying))
            {
                // Conditions improved, plant recovers
                _activePlants[pos] = (plant, status & ~PlantStatus.Dying);
            }
        });

        EventManager.Emit(new MoistureRequest { Position = pos, CallbackId = callbackId });
    }

    private void KillPlant(Position pos)
    {
        WorldApi.ModifyTile(pos, surface: TileSurface.None);
        _activePlants.Remove(pos);
        _logger.LogDebug($"Plant died at {pos}");
    }

    private static Position[] GetSeedPositions(Position origin, byte range)
    {
        var positions = new List<Position>();
        for(int x = -range; x <= range; x++)
        {
            for(int y = -range; y <= range; y++)
            {
                if(Math.Abs(x) + Math.Abs(y) <= range) // Diamond shape spread
                {
                    positions.Add(origin + (x, y));
                }
            }
        }
        return positions.ToArray();
    }

    public override void Destroy()
    {
        EventManager.UnregisterListener<PlantReproductionEvent>(this);
        EventManager.UnregisterListener<MoistureChangeEvent>(this);
    }

    public override void HandleMessage(IEvent evt)
    {
        
    }
}
