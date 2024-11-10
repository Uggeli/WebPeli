using System.Collections.Concurrent;
using WebPeli.GameEngine.EntitySystem;
namespace WebPeli.GameEngine.Managers;
public readonly record struct EntityPosition(int X, int Y);

public class Chunk
{
    private readonly byte[,] tiles = new byte[Config.CHUNK_SIZE, Config.CHUNK_SIZE];
    private readonly byte[,] tileTextures = new byte[Config.CHUNK_SIZE, Config.CHUNK_SIZE];  // Later: used for rendering
    // x, y, tile
    // tile: first 4 bits is reserved for chunkexit data, last 4 bits is tile data
    // chunkexit data: booleans; exit_north, exit_east, exit_south, exit_west
    // tile data: bool is_traversable, bool is_transparent, byte tile_type: 0-3
    private readonly Dictionary<EntityPosition, HashSet<Guid>> _positionMap = [];
    private readonly Dictionary<Guid, HashSet<EntityPosition>> _entityPositions = [];
    public bool AddEntity(Guid entityId, IEnumerable<EntityPosition> positions)
    {
        if (HasCollision(positions)) return false;

        var positionsSet = new HashSet<EntityPosition>(positions);
        _entityPositions[entityId] = positionsSet;

        foreach (var pos in positions)
        {
            if (!_positionMap.TryGetValue(pos, out var entities))
            {
                entities = [];
                _positionMap[pos] = entities;
            }
            entities.Add(entityId);
        }
        return true;
    }

    public bool RemoveEntity(Guid entityId)
    {
        if (!_entityPositions.TryGetValue(entityId, out var positions))
            return false;

        foreach (var pos in positions)
        {
            if (_positionMap.TryGetValue(pos, out var entities))
            {
                entities.Remove(entityId);
                if (entities.Count == 0)
                    _positionMap.Remove(pos);
            }
        }
        _entityPositions.Remove(entityId);

        return true;
    }

    public IEnumerable<Guid> GetEntitiesAt(EntityPosition position) =>
        _positionMap.TryGetValue(position, out var entities) ? entities : [];

    public bool UpdateEntityPosition(Guid entityId, EntityPosition newPos)
    {
        if (!_entityPositions.ContainsKey(entityId)) return false;
        return UpdateEntityPositions(entityId, [newPos]);
    }

    public bool UpdateEntityPositions(Guid entityId, IEnumerable<EntityPosition> newPositions)
    {
        if (!ValidatePositions(newPositions)) return false;
        if (HasCollision(newPositions)) return false;

        RemoveEntity(entityId);
        return AddEntity(entityId, newPositions);
    }

    private static bool ValidatePositions(IEnumerable<EntityPosition> positions)
    {
        foreach (var pos in positions)
        {
            if (!IsInBounds(pos.X, pos.Y)) return false;
        }
        return true;
    }

    private bool HasCollision(IEnumerable<EntityPosition> positions)
    {
        return positions.Any(pos => 
            !IsTraversable(pos) || 
            (_positionMap.ContainsKey(pos) && _positionMap[pos].Count > 0));
    }


    public void SetTile(byte x, byte y, byte tileData)
    {
        if (!IsInBounds(x, y)) return;
        tiles[x, y] = tileData;
    }

    public byte GetTile(byte x, byte y)
    {
        if (!IsInBounds(x, y)) return 0;
        return tiles[x, y];
    }

    static bool IsInBounds(byte x, byte y) => x >= 0 && x < Config.CHUNK_SIZE && y >= 0 && y < Config.CHUNK_SIZE;
    static bool IsInBounds(int x, int y) => x >= 0 && x < Config.CHUNK_SIZE && y >= 0 && y < Config.CHUNK_SIZE;


    public byte this[byte x, byte y]
    {
        get => GetTile(x, y);
        set => SetTile(x, y, value);
    }

    // Chunk exit methods
    public bool GetExitNorth(byte x, byte y) => (GetTile(x, y) & 0b00000001) != 0;
    public bool GetExitEast(byte x, byte y) => (GetTile(x, y) & 0b00000010) != 0;
    public bool GetExitSouth(byte x, byte y) => (GetTile(x, y) & 0b00000100) != 0;
    public bool GetExitWest(byte x, byte y) => (GetTile(x, y) & 0b00001000) != 0;

    public void SetExitNorth(byte x, byte y, bool value) => SetBit(x, y, 0, value);
    public void SetExitEast(byte x, byte y, bool value) => SetBit(x, y, 1, value);
    public void SetExitSouth(byte x, byte y, bool value) => SetBit(x, y, 2, value);
    public void SetExitWest(byte x, byte y, bool value) => SetBit(x, y, 3, value);

    // Tile property methods
    public bool IsTraversable(byte x, byte y) => (GetTile(x, y) & 0b00010000) != 0;
    public bool IsTraversable(EntityPosition pos) => IsTraversable((byte)pos.X, (byte)pos.Y);
    public bool IsTransparent(byte x, byte y) => (GetTile(x, y) & 0b00100000) != 0;
    public byte GetTileType(byte x, byte y) => (byte)((GetTile(x, y) & 0b11000000) >> 6);

    public void SetTraversable(byte x, byte y, bool value) => SetBit(x, y, 4, value);
    public void SetTransparent(byte x, byte y, bool value) => SetBit(x, y, 5, value);
    public void SetTileType(byte x, byte y, byte tileType)
    {
        if (tileType > 3) throw new ArgumentOutOfRangeException(nameof(tileType), "Tile type must be 0-3");
        byte tile = GetTile(x, y);
        tile = (byte)((tile & 0b00111111) | (tileType << 6));
        SetTile(x, y, tile);
    }

    private void SetBit(byte x, byte y, int bitPosition, bool value)
    {
        byte tile = GetTile(x, y);
        if (value)
            tile |= (byte)(1 << bitPosition);
        else
            tile &= (byte)~(1 << bitPosition);
        SetTile(x, y, tile);
    }

    private void GenerateChunk()
    {
        for (byte x = 0; x < Config.CHUNK_SIZE; x++)
        {
            for (byte y = 0; y < Config.CHUNK_SIZE; y++)
            {
                byte tile = 0b00010000; // Empty walkable tile
                SetTile(x, y, tile);
            }
        }
    }

    public (byte, byte)[] GetPath(byte startX, byte startY, byte endX, byte endY)
    {
        // A* pathfinding
        var openSet = new PriorityQueue<(byte X, byte Y), float>();
        var cameFrom = new Dictionary<(byte X, byte Y), (byte X, byte Y)>();

        var gScore = new Dictionary<(byte X, byte Y), float>();
        var fScore = new Dictionary<(byte X, byte Y), float>();

        openSet.Enqueue((startX, startY), 0);
        gScore[(startX, startY)] = 0;
        fScore[(startX, startY)] = Heuristic((startX, startY), (endX, endY));

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current == (endX, endY))
            {
                var path = new List<(byte X, byte Y)>
                {
                    current
                };
                while (cameFrom.ContainsKey(current))
                {
                    current = cameFrom[current];
                    path.Add(current);
                }
                path.Reverse();
                path.RemoveAt(0); // Remove the starting position
                return [.. path];
            }

            foreach (var neighbour in GetNeighbours(current.X, current.Y))
            {
                var tentativeGScore = gScore[current] + 1; // 1 is the distance between two nodes
                if (!gScore.TryGetValue(neighbour, out float value) || tentativeGScore < value)
                {
                    cameFrom[neighbour] = current;
                    value = tentativeGScore;
                    gScore[neighbour] = value;
                    fScore[neighbour] = gScore[neighbour] + Heuristic(neighbour, (endX, endY));
                    openSet.Enqueue(neighbour, fScore[neighbour]);
                }
            }
        }
        return [];
    }

    private (byte X, byte Y)[] GetNeighbours(byte x, byte y)
    {
        const byte MAX = (byte)(Config.CHUNK_SIZE - 1);
        var neighbours = new List<(byte X, byte Y)>(4); // Pre-allocate for max possible neighbours

        // Define potential neighbours
        (byte X, byte Y)[] potentialNeighbours = [
            (x, (byte)(y - 1)), // North
            ((byte)(x + 1), y), // East
            (x, (byte)(y + 1)), // South
            ((byte)(x - 1), y)  // West
        ];

        foreach (var (nx, ny) in potentialNeighbours)
        {
            if (nx <= MAX && ny <= MAX && IsInBounds(nx, ny) && IsTraversable(nx, ny))
            {
                neighbours.Add((nx, ny));
            }
        }

        return [.. neighbours];
    }

    private static float Heuristic((byte X, byte Y) a, (byte X, byte Y) b)
    {
        return MathF.Abs(a.X - b.X) + MathF.Abs(a.Y - b.Y); // Manhattan distance
    }

    private void BuildChunkExits()
    {
        var NorthEdgeTiles = new List<(byte X, byte Y)>();
        var EastEdgeTiles = new List<(byte X, byte Y)>();
        var SouthEdgeTiles = new List<(byte X, byte Y)>();
        var WestEdgeTiles = new List<(byte X, byte Y)>();

        for (byte x = 0; x < Config.CHUNK_SIZE; x++)
        {
            for (byte y = 0; y < Config.CHUNK_SIZE; y++)
            {
                if (!IsTraversable(x, y)) continue;
                if (y == 0) NorthEdgeTiles.Add((x, y));
                if (x == Config.CHUNK_SIZE - 1) EastEdgeTiles.Add((x, y));
                if (y == Config.CHUNK_SIZE - 1) SouthEdgeTiles.Add((x, y));
                if (x == 0) WestEdgeTiles.Add((x, y));
            }
        }

        void value(int x, ParallelLoopState loopState)
        {
            for (byte y = 0; y < Config.CHUNK_SIZE; y++)
            {
                // only check on actual edge tiles
                if (x != 0 && x != Config.CHUNK_SIZE - 1 && y != 0 && y != Config.CHUNK_SIZE - 1) continue;

                if (IsConnectedToEdge((byte)x, y, NorthEdgeTiles))
                {
                    SetExitNorth((byte)x, y, true);
                }
                if (IsConnectedToEdge((byte)x, y, EastEdgeTiles))
                {
                    SetExitEast((byte)x, y, true);
                }
                if (IsConnectedToEdge((byte)x, y, SouthEdgeTiles))
                {
                    SetExitSouth((byte)x, y, true);
                }
                if (IsConnectedToEdge((byte)x, y, WestEdgeTiles))
                {
                    SetExitWest((byte)x, y, true);
                }
            }
        }
        Parallel.For(0, Config.CHUNK_SIZE, value);
    }

    private bool IsConnectedToEdge(byte x, byte y, List<(byte X, byte Y)> edgeTiles)
    {
        foreach (var (ex, ey) in edgeTiles)
        {
            if (GetPath(x, y, ex, ey).Length > 0)
            {
                return true;
            }
        }
        return false;
    }

    public Chunk()
    {
        GenerateChunk();
        BuildChunkExits();
    }
}
