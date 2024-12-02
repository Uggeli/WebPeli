using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World;
using WebPeli.GameEngine.World.WorldData;

namespace WebPeli.GameEngine.Managers;
public class MapManager : BaseManager
{
    private readonly byte[] _moisture = new byte[Config.WORLD_TILES * Config.WORLD_TILES];
    private static int WorldToIndex(int x, int y) => y * Config.WORLD_TILES + x;
    
    public override void Init()
    {
        EventManager.RegisterListener<MoistureChangeEvent>(this);
        EventManager.RegisterListener<AreaMoistureChangeEvent>(this);

        HashSet<Position> waterTiles = [];
        // Initialize water tiles to max moisture
        for (int x = 0; x < Config.WORLD_TILES - 1; x++)
        {
            for (int y = 0; y < Config.WORLD_TILES - 1; y++)
            {
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
        }
    }

    private void HandleMoistureChange(Position pos, byte amount)
    {
        var worldIdx = WorldToIndex(pos.X, pos.Y);
        var tile = WorldApi.GetTileInfo(pos);
        
        // Get material properties
        var (capacity, absorption) = TileManager.GetMaterialMoistureProperties(tile.material);
        
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
        throw new NotImplementedException();
    }
}