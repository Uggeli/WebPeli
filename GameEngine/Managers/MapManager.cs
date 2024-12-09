using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World;
using WebPeli.GameEngine.World.WorldData;

namespace WebPeli.GameEngine.Managers;
public class MapManager(ILogger<MapManager> logger) : BaseManager
{
    private readonly ILogger<MapManager> _logger = logger;
    private readonly byte[] _moisture = new byte[Config.WORLD_TILES * Config.WORLD_TILES];
    private readonly byte[] _temperature = new byte[Config.WORLD_TILES * Config.WORLD_TILES];
    private readonly byte[] _sunlight = new byte[Config.WORLD_TILES * Config.WORLD_TILES]; // 0 full exposure, 255 full shade
    private static int WorldToIndex(int x, int y) => y * Config.WORLD_TILES + x;
    
    public override void Init()
    {
        EventManager.RegisterListener<MoistureChangeEvent>(this);
        EventManager.RegisterListener<AreaMoistureChangeEvent>(this);
        EventManager.RegisterListener<AreaTemperatureChangeEvent>(this);
        // EventManager.RegisterListener<AreaSunlightChangeEvent>(this);
        EventManager.RegisterListener<MoistureRequest>(this);

        HashSet<Position> waterTiles = [];
        // Initialize water tiles to max moisture
        for (int x = 0; x < Config.WORLD_TILES - 1; x++)
        {
            for (int y = 0; y < Config.WORLD_TILES - 1; y++)
            {
                _moisture[WorldToIndex(x, y)] = 30; // lets set some initial moisture
                var pos = new Position(x, y);
                var (material, _, _) = WorldApi.GetTileInfo(pos);
                if (material == TileMaterial.Water)
                {
                    _moisture[WorldToIndex(x, y)] = Config.WATER_TILE_MOISTURE;
                    waterTiles.Add(pos);
                }
            }
        }

        // Spread moisture from water tiles
        foreach (var pos in waterTiles)
        {
            SpreadMoisture(pos, Config.WATER_TILE_MOISTURE);
        }
        // lets spread moisture again
        
        _logger.LogInformation("Map manager initialized");
        _logger.LogInformation("{0} water tiles", waterTiles.Count);
        var moistTiles = 0;
        for (int i = 0; i < _moisture.Length; i++)
        {
            if (_moisture[i] > 0)
            {
                moistTiles++;
            }
        }
        _logger.LogInformation("{0} moist tiles", moistTiles - waterTiles.Count);
    }

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case MoistureChangeEvent change:
                HandleMoistureChange(change.Position, change.Amount);
                break;
            case AreaMoistureChangeEvent areaChange:
                HandleAreaMoistureChange(areaChange);
                break;
            case AreaTemperatureChangeEvent areaChange:
                HandleAreaTemperatureChange(areaChange);
                break;
            // case AreaSunlightChangeEvent areaChange:
            //     HandleAreaSunlightChange(areaChange);
            //     break;
            case MoistureRequest request:
                HandleMoistureRequest(request);
                break;
        }
    }

    private void HandleAreaTemperatureChange(AreaTemperatureChangeEvent areaChange)
    {
        throw new NotImplementedException();
    }


    private void HandleMoistureRequest(MoistureRequest request)
    {
        // var (material, _, _) = WorldApi.GetTileInfo(request.Position);
        var moisture = _moisture[WorldToIndex(request.Position.X, request.Position.Y)];
        var callbackId = request.CallbackId;
        EventManager.EmitCallback(callbackId, moisture);
    }

    private void HandleMoistureChange(Position pos, byte amount)
    {
        var worldIdx = WorldToIndex(pos.X, pos.Y);
        var (material, _, _) = WorldApi.GetTileInfo(pos);
        
        // Get material properties
        var (capacity, absorption) = TileManager.GetMaterialMoistureProperties(material);
        
        // Calculate how much moisture tile can absorb
        var current = _moisture[worldIdx];
        var absorbable = Math.Min(amount, capacity - current);
        var absorbed = (byte)Math.Min(absorbable, absorption);
        
        // If we absorbed anything, update tile
        if (absorbed > 0)
        {
            _moisture[worldIdx] += absorbed;
            amount -= absorbed;
        }

        // If we have excess moisture, spread to neighbors
        if (amount > Config.MOISTURE_MIN_DIFFERENCE)
        {
            SpreadMoisture(pos, amount);
        }
    }

    private void HandleAreaMoistureChange(AreaMoistureChangeEvent areaChange)
    {

    }

    private void SpreadMoisture(Position pos, byte amount)
    {
        // Get valid neighbors
        var neighbors = GetValidNeighbors(pos);
        if (neighbors.Count == 0) return;

        // Calculate amount per neighbor
        byte amountPerNeighbor = (byte)(amount / neighbors.Count);
        if (amountPerNeighbor < Config.MOISTURE_MIN_DIFFERENCE) return;

        // Spread to each neighbor
        foreach (var neighbor in neighbors)
        {
            HandleMoistureChange(neighbor, amountPerNeighbor);
        }
    }

    private static List<Position> GetValidNeighbors(Position pos)
    {
        var neighbors = new List<Position>();
        foreach (var dir in pos.GetNeighbours())
        {
            var neighbor = pos + dir;
            if (WorldApi.IsInWorldBounds(neighbor))
            {
                neighbors.Add(neighbor);
            }
        }
        return neighbors;
    }

    public override void Destroy()
    {
        EventManager.UnregisterListener<MoistureChangeEvent>(this);
        EventManager.UnregisterListener<AreaMoistureChangeEvent>(this);
        EventManager.UnregisterListener<AreaTemperatureChangeEvent>(this);
        // EventManager.UnregisterListener<AreaSunlightChangeEvent>(this);
        EventManager.UnregisterListener<MoistureRequest>(this);
    }
}