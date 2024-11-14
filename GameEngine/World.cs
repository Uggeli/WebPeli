using System.Collections.Concurrent;
using System.Numerics;
using System.Text;
using WebPeli.GameEngine.EntitySystem;
using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Util;

namespace WebPeli.GameEngine;


public enum CurrentAction : byte
{
    Idle,
    Moving,
    Attacking,
}

public readonly record struct EntityState
{
    public EntityPosition Position { get; init; }  // in world coordinates
    public float Rotation { get; init; }  // Added rotation in radians
    public CurrentAction Current { get; init; }
}

// Add this struct to hold entity cell data
public readonly record struct EntityGridCell
{
    public int Count { get; init; }
    public CurrentAction Action { get; init; }
    public float Rotation { get; init; }
}

public readonly record struct EntityPosition(int X, int Y)
{
    public (byte ChunkX, byte ChunkY, byte LocalX, byte LocalY) ToChunkSpace()
    {
        return (
            (byte)(X / Config.CHUNK_SIZE),
            (byte)(Y / Config.CHUNK_SIZE),
            (byte)(X % Config.CHUNK_SIZE),
            (byte)(Y % Config.CHUNK_SIZE)
        );
    }
}

public static class World
{

    public readonly record struct ChunkExit
    {
        public required byte X { get; init; }
        public required byte Y { get; init; }
        public required Direction Direction { get; init; }
    }

    private static readonly ConcurrentDictionary<(int ChunkX, int ChunkY), List<ChunkExit>> _chunkExits = [];

    private static Chunk[,] _chunks { get; set; } = new Chunk[Config.WORLD_SIZE, Config.WORLD_SIZE];
    private static ConcurrentDictionary<Guid, EntityState> _entityStates = [];

    // Entity methods
    public static void SetEntityState(Guid entityId, EntityState state)
    {
        _entityStates.AddOrUpdate(entityId, state, (_, _) => state);
    }

    public static EntityState? GetEntityState(Guid entityId)
    {
        return _entityStates.TryGetValue(entityId, out EntityState state) ? state : null;
    }

    public static IEnumerable<Guid> GetEntitiesAt(Vector2 position)
    {
        var (chunkX, chunkY, localX, localY) = new EntityPosition((int)position.X, (int)position.Y).ToChunkSpace();
        return GetChunk(chunkX, chunkY)?.GetEntitiesAt((localX, localY)) ?? Enumerable.Empty<Guid>();
    }

    public static void UpdateEntityPosition(Guid entityId, EntityPosition position)
    {
        var (chunkX, chunkY, localX, localY) = position.ToChunkSpace();
        var chunk = GetChunk(chunkX, chunkY);
        if (chunk == null) return;

        var state = GetEntityState(entityId);
        SetEntityState(entityId, new EntityState
        {
            Position = position,
            Rotation = state?.Rotation ?? 0f,
            Current = state?.Current ?? CurrentAction.Idle
        });

        chunk.UpdateEntityPosition(entityId, (localX, localY));
    }

    public static bool RemoveEntity(Guid entityId)
    {
        _entityStates.TryRemove(entityId, out _);
        for (byte x = 0; x < Config.WORLD_SIZE; x++)
        {
            for (byte y = 0; y < Config.WORLD_SIZE; y++)
            {
                if (_chunks[x, y]?.RemoveEntity(entityId) == true)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static void AddEntity(Guid entityId, EntityPosition position)
    {
        var (chunkX, chunkY, localX, localY) = position.ToChunkSpace();
        GetChunk(chunkX, chunkY)?.AddEntity(entityId, [(localX, localY)]);
    }

    public static void AddEntity(Guid entityId)
    {
        var random = new Random();
        var position = new EntityPosition(
            random.Next(0, Config.WORLD_SIZE * Config.CHUNK_SIZE),
            random.Next(0, Config.WORLD_SIZE * Config.CHUNK_SIZE)
        );
        SetEntityState(entityId, new EntityState
        {
            Position = position,
            Rotation = 0f,
            Current = CurrentAction.Idle
        });
        AddEntity(entityId, position);
    }

    // Chunk methods
    public static Chunk? GetChunk(byte x, byte y)
    {
        return IsInWorldBounds(x, y) ? _chunks[x, y] : null;
    }

    public static void SetChunk(byte x, byte y, Chunk chunk)
    {
        if (IsInWorldBounds(x, y))
        {
            _chunks[x, y] = chunk;
        }
    }

    private static bool IsInWorldBounds(byte x, byte y)
    {
        return x < Config.WORLD_SIZE && y < Config.WORLD_SIZE;
    }

    // Essentially zoom level for the viewport
    private const float MIN_VIEWPORT_SIZE = 100;  // pixels
    private const float MAX_VIEWPORT_SIZE = 2000; // pixels

    public static byte[,] GetTilesInArea(float screenX, float screenY, float viewportWidth, float viewportHeight, float? worldWidth = null, float? worldHeight = null)
    {
        // Same viewport size validation as before
        viewportWidth = Math.Clamp(viewportWidth, MIN_VIEWPORT_SIZE, MAX_VIEWPORT_SIZE);
        viewportHeight = Math.Clamp(viewportHeight, MIN_VIEWPORT_SIZE, MAX_VIEWPORT_SIZE);

        var (startWorldX, startWorldY) = Util.CoordinateSystem.ScreenToWorld(
            screenX, screenY,
            viewportWidth, viewportHeight,
            worldWidth, worldHeight);
        var (endWorldX, endWorldY) = Util.CoordinateSystem.ScreenToWorld(
            screenX + viewportWidth, screenY + viewportHeight,
            viewportWidth, viewportHeight,
            worldWidth, worldHeight);

        int gridWidth = Math.Abs(endWorldX - startWorldX) + 1;
        int gridHeight = Math.Abs(endWorldY - startWorldY) + 1;
        var tileGrid = new byte[gridWidth, gridHeight];

        // Now matches the tile format from WorldGenerator:
        // - bits 0-1: tile type (0-3): water, grass, hills, mountains
        // - bit 6: transparent flag
        // - bit 7: traversable flag
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                int worldX = startWorldX + x;
                int worldY = startWorldY + y;

                var (chunkX, chunkY, localX, localY) =
                    Util.CoordinateSystem.WorldToChunkAndLocal(worldX, worldY);

                var chunk = World.GetChunk(chunkX, chunkY);
                if (chunk != null)
                {
                    byte tileData = 0;
                    if (chunk.IsTraversable(localX, localY))
                        tileData |= 0b10000000;
                    if (chunk.IsTransparent(localX, localY))
                        tileData |= 0b01000000;
                    tileData |= chunk.GetTileType(localX, localY); // Already 0-3

                    tileGrid[x, y] = tileData;
                }
                else
                {
                    tileGrid[x, y] = 0xFF; // Out of bounds 
                }
            }
        }

        return tileGrid;
    }

    public static EntityGridCell[,] GetEntitiesInArea(float screenX, float screenY, float viewportWidth, float viewportHeight, float? worldWidth = null, float? worldHeight = null)
    {
        // Same viewport size validation as before
        viewportWidth = Math.Clamp(viewportWidth, MIN_VIEWPORT_SIZE, MAX_VIEWPORT_SIZE);
        viewportHeight = Math.Clamp(viewportHeight, MIN_VIEWPORT_SIZE, MAX_VIEWPORT_SIZE);

        var (startWorldX, startWorldY) = Util.CoordinateSystem.ScreenToWorld(
            screenX, screenY,
            viewportWidth, viewportHeight,
            worldWidth, worldHeight);
        var (endWorldX, endWorldY) = Util.CoordinateSystem.ScreenToWorld(
            screenX + viewportWidth, screenY + viewportHeight,
            viewportWidth, viewportHeight,
            worldWidth, worldHeight);

        int gridWidth = Math.Abs(endWorldX - startWorldX) + 1;
        int gridHeight = Math.Abs(endWorldY - startWorldY) + 1;
        var entityGrid = new EntityGridCell[gridWidth, gridHeight];

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                int worldX = startWorldX + x;
                int worldY = startWorldY + y;

                var entities = GetEntitiesAt(new Vector2(worldX, worldY)).ToList();
                if (entities.Count > 0)
                {
                    // Get state of first entity at this position
                    var firstEntity = entities.First();
                    var state = GetEntityState(firstEntity);

                    entityGrid[x, y] = new EntityGridCell
                    {
                        Count = entities.Count,
                        Action = state?.Current ?? CurrentAction.Idle,
                        Rotation = state?.Rotation ?? 0f
                    };
                }
            }
        }

        return entityGrid;
    }

    private static (byte X, byte Y)[] GetNeighbors(Chunk chunk, byte x, byte y)
    {
        const byte MAX = Config.CHUNK_SIZE - 1;
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
            if (nx <= MAX && ny <= MAX && IsInChunkBounds(nx, ny) && chunk.IsTraversable(nx, ny))
            {
                neighbours.Add((nx, ny));
            }
        }

        return [.. neighbours];
    }

    public static bool IsInChunkBounds(byte x, byte y) => x >= 0 && x < Config.CHUNK_SIZE && y >= 0 && y < Config.CHUNK_SIZE;
    public static bool IsInChunkBounds(int x, int y) => x >= 0 && x < Config.CHUNK_SIZE && y >= 0 && y < Config.CHUNK_SIZE;

    private static float Heuristic((byte X, byte Y) a, (byte X, byte Y) b)
    {
        return MathF.Abs(a.X - b.X) + MathF.Abs(a.Y - b.Y); // Manhattan distance
    }

    public static void BuildChunkExits(Chunk chunk)
    {
        // First find all traversable edge tiles and group by direction
        var potentialExits = GetPotentialExits(chunk);

        // Then verify which ones are actually accessible
        var validExits = new List<ChunkExit>();
        var visited = new bool[Config.CHUNK_SIZE, Config.CHUNK_SIZE];

        foreach (var exit in potentialExits)
        {
            // Skip if we already found this tile is accessible 
            if (visited[exit.X, exit.Y]) continue;

            // Do flood fill from this exit
            if (FloodFillFromExit(chunk, exit.X, exit.Y, ref visited))
            {
                validExits.Add(exit);
                // Mark the actual exit in chunk data
                SetExitForDirection(chunk, exit);
            }
        }

        // Cache the validated exits for this chunk
        _chunkExits[(chunk.X, chunk.Y)] = validExits;
    }

    private static List<ChunkExit> GetPotentialExits(Chunk chunk)
    {
        var exits = new List<ChunkExit>();

        // Check North & South edges
        for (byte x = 0; x < Config.CHUNK_SIZE; x++)
        {
            if (chunk.IsTraversable(x, 0))
                exits.Add(new ChunkExit { X = x, Y = 0, Direction = Direction.Up });

            if (chunk.IsTraversable(x, Config.CHUNK_SIZE - 1))
                exits.Add(new ChunkExit { X = x, Y = Config.CHUNK_SIZE - 1, Direction = Direction.Down });
        }

        // Check East & West edges
        for (byte y = 0; y < Config.CHUNK_SIZE; y++)
        {
            if (chunk.IsTraversable(0, y))
                exits.Add(new ChunkExit { X = 0, Y = y, Direction = Direction.Left });

            if (chunk.IsTraversable(Config.CHUNK_SIZE - 1, y))
                exits.Add(new ChunkExit { X = Config.CHUNK_SIZE - 1, Y = y, Direction = Direction.Right });
        }

        return exits;
    }

    private static bool FloodFillFromExit(Chunk chunk, byte startX, byte startY, ref bool[,] visited)
    {
        var queue = new Queue<(byte X, byte Y)>();
        queue.Enqueue((startX, startY));

        bool foundInterior = false;

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();

            if (visited[x, y]) continue;
            visited[x, y] = true;

            // If we find a non-edge traversable tile, this exit is valid
            if (!IsEdgeTile(x, y) && chunk.IsTraversable(x, y))
            {
                foundInterior = true;
                // Don't break - continue filling to mark other accessible tiles
            }

            // Check all neighbors
            foreach (var (nx, ny) in GetNeighbors(chunk, x, y))
            {
                if (!visited[nx, ny] && chunk.IsTraversable(nx, ny))
                {
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return foundInterior;
    }

    private static bool IsEdgeTile(byte x, byte y) =>
        x == 0 || x == Config.CHUNK_SIZE - 1 || y == 0 || y == Config.CHUNK_SIZE - 1;

    private static void SetExitForDirection(Chunk chunk, ChunkExit exit)
    {
        switch (exit.Direction)
        {
            case Direction.Up: chunk.SetExitNorth(exit.X, exit.Y, true); break;
            case Direction.Right: chunk.SetExitEast(exit.X, exit.Y, true); break;
            case Direction.Down: chunk.SetExitSouth(exit.X, exit.Y, true); break;
            case Direction.Left: chunk.SetExitWest(exit.X, exit.Y, true); break;
        }
    }

    private static Direction GetDirectionFromEdge(byte x, byte y)
    {
        if (x == 0) return Direction.Left;
        if (x == Config.CHUNK_SIZE - 1) return Direction.Right;
        if (y == 0) return Direction.Up;
        if (y == Config.CHUNK_SIZE - 1) return Direction.Down;
        return Direction.None;
    }



    private static (byte X, byte Y) GetNeighborChunk(byte x, byte y, Direction direction)
    {
        return direction switch
        {
            Direction.Up => (x, (byte)(y - 1)),
            Direction.Right => ((byte)(x + 1), y),
            Direction.Down => (x, (byte)(y + 1)),
            Direction.Left => ((byte)(x - 1), y),
            _ => (x, y)
        };
    }

    private static (byte chunkX, byte chunkY)[] GetChunkPath(byte startChunkX, byte startChunkY, byte targetChunkX, byte targetChunkY)
    {
        if (startChunkX == targetChunkX && startChunkY == targetChunkY)
            return [(startChunkX, startChunkY)];

        var openSet = new PriorityQueue<(byte x, byte y), float>();
        var cameFrom = new Dictionary<(byte x, byte y), (byte x, byte y)>();
        var gScore = new Dictionary<(byte x, byte y), float>();

        openSet.Enqueue((startChunkX, startChunkY), 0);
        gScore[(startChunkX, startChunkY)] = 0;

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current.x == targetChunkX && current.y == targetChunkY)
            {
                return ReconstructPath(cameFrom, current);
            }

            // Get valid neighbors based on chunk exits
            if (!_chunkExits.TryGetValue((current.x, current.y), out var exits))
                continue;

            foreach (var exit in exits)
            {
                // Determine neighbor chunk based on exit direction
                var (x, y) = GetNeighborFromExit(current.x, current.y, exit.Direction);
                if (!IsInWorldBounds(x, y))
                    continue;

                var tentativeScore = gScore[(current.x, current.y)] + 1;

                if (!gScore.ContainsKey((x, y)) ||
                    tentativeScore < gScore[(x, y)])
                {
                    cameFrom[(x, y)] = (current.x, current.y);
                    gScore[(x, y)] = tentativeScore;
                    var priority = tentativeScore + Heuristic((x, y), (targetChunkX, targetChunkY));
                    openSet.Enqueue((x, y), priority);
                }
            }
        }

        return []; // No path found
    }

    private static List<Zone> GetZonePath(int startX, int startY, int targetX, int targetY, Chunk startChunk, Chunk? targetChunk)
    {
        // Get zones containing start and target positions
        var startZone = FindZoneContainingPoint(startX, startY, startChunk);
        var targetZone = FindZoneContainingPoint(targetX, targetY, targetChunk ?? startChunk);

        if (startZone is not Zone start || targetZone is not Zone target)
            return [];

        if (start.Id == target.Id)
            return [start];

        var openSet = new PriorityQueue<int, float>();  // Queue of zone IDs
        var cameFrom = new Dictionary<int, int>();      // Zone ID -> Previous Zone ID
        var gScore = new Dictionary<int, float>();      // Known costs

        openSet.Enqueue(start.Id, 0);
        gScore[start.Id] = 0;

        while (openSet.Count > 0)
        {
            var currentZoneId = openSet.Dequeue();
            var currentZone = ZoneManager.GetZone(currentZoneId);

            if (currentZone is not Zone current)
                continue;

            if (currentZoneId == target.Id)
            {
                return ReconstructZonePath(cameFrom, start.Id, target.Id);
            }

            // Check all adjacent zones
            foreach (var (adjZoneId, cost) in current.AdjacentZones)
            {
                var tentativeScore = gScore[currentZoneId] + cost;

                if (!gScore.ContainsKey(adjZoneId) || tentativeScore < gScore[adjZoneId])
                {
                    cameFrom[adjZoneId] = currentZoneId;
                    gScore[adjZoneId] = tentativeScore;

                    var heuristic = EstimateZoneDistance(
                        ZoneManager.GetZone(adjZoneId),
                        target
                    );

                    openSet.Enqueue(adjZoneId, tentativeScore + heuristic);
                }
            }
        }

        return []; // No path found
    }

    private static Zone? FindZoneContainingPoint(int x, int y, Chunk chunk)
    {
        var zones = ZoneManager.GetZonesInChunk(chunk.X, chunk.Y);
        return zones.FirstOrDefault(zone =>
            zone.Tiles.Contains(((byte)x, (byte)y))
        );
    }

    private static List<Zone> ReconstructZonePath(
        Dictionary<int, int> cameFrom,
        int startZoneId,
        int currentZoneId)
    {
        var path = new List<Zone>();
        var current = currentZoneId;

        while (current != startZoneId)
        {
            if (ZoneManager.GetZone(current) is Zone zone)
                path.Add(zone);

            if (!cameFrom.ContainsKey(current))
                break;

            current = cameFrom[current];
        }

        if (ZoneManager.GetZone(startZoneId) is Zone startZone)
            path.Add(startZone);

        path.Reverse();
        return path;
    }

    private static float EstimateZoneDistance(Zone? a, Zone? b)
    {
        if (a == null || b == null)
            return float.MaxValue;

        // Use center points of zones as reference
        var centerA = GetZoneCenter(a.Value.Tiles);
        var centerB = GetZoneCenter(b.Value.Tiles);

        return MathF.Sqrt(
            MathF.Pow(centerA.X - centerB.X, 2) +
            MathF.Pow(centerA.Y - centerB.Y, 2)
        );
    }

    private static (float X, float Y) GetZoneCenter(HashSet<(byte X, byte Y)> tiles)
    {
        if (tiles.Count == 0)
            return (0, 0);

        float sumX = 0, sumY = 0;
        foreach (var (x, y) in tiles)
        {
            sumX += x;
            sumY += y;
        }

        return (sumX / tiles.Count, sumY / tiles.Count);
    }

    private static (byte x, byte y)[] GetTilePath(byte startX, byte startY, byte targetX, byte targetY, List<Zone> zonePath)
    {
        if (zonePath.Count == 0)
            return [];

        var openSet = new PriorityQueue<(byte x, byte y), float>();
        var cameFrom = new Dictionary<(byte x, byte y), (byte x, byte y)>();
        var gScore = new Dictionary<(byte x, byte y), float>();

        // Create set of allowed tiles from zones for quick lookup
        var allowedTiles = new HashSet<(byte x, byte y)>();
        foreach (var zone in zonePath)
        {
            foreach (var tile in zone.Tiles)
            {
                allowedTiles.Add(tile);
            }
        }

        openSet.Enqueue((startX, startY), 0);
        gScore[(startX, startY)] = 0;

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current == (targetX, targetY))
            {
                return ReconstructTilePath(cameFrom, current);
            }

            // Get neighbors but only consider ones in allowed tiles
            foreach (var neighbor in GetTileNeighbors(current.x, current.y))
            {
                if (!allowedTiles.Contains(neighbor))
                    continue;

                var tentativeScore = gScore[current] + 1; // Using 1 as base cost

                if (!gScore.ContainsKey(neighbor) || tentativeScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeScore;
                    var priority = tentativeScore + TileHeuristic(neighbor, (targetX, targetY));
                    openSet.Enqueue(neighbor, priority);
                }
            }
        }

        return [];
    }

    private static (byte x, byte y)[] GetTileNeighbors(byte x, byte y)
    {
        var neighbors = new List<(byte x, byte y)>();

        // Check all 4 directions
        if (x > 0) neighbors.Add(((byte)(x - 1), y));
        if (x < Config.CHUNK_SIZE - 1) neighbors.Add(((byte)(x + 1), y));
        if (y > 0) neighbors.Add((x, (byte)(y - 1)));
        if (y < Config.CHUNK_SIZE - 1) neighbors.Add((x, (byte)(y + 1)));

        return [.. neighbors];
    }

    private static float TileHeuristic((byte x, byte y) a, (byte x, byte y) target)
    {
        return MathF.Abs(a.x - target.x) + MathF.Abs(a.y - target.y); // Manhattan distance
    }

    private static (byte x, byte y)[] ReconstructTilePath(
        Dictionary<(byte x, byte y), (byte x, byte y)> cameFrom,
        (byte x, byte y) current)
    {
        var path = new List<(byte x, byte y)> { current };

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return [.. path];
    }

    public static (int x, int y)[] FindPath(int startX, int startY, int targetX, int targetY)
    {
        var (startChunkX, startChunkY, startLocalX, startLocalY) = Util.CoordinateSystem.WorldToChunkAndLocal(startX, startY);
        var (targetChunkX, targetChunkY, targetLocalX, targetLocalY) = Util.CoordinateSystem.WorldToChunkAndLocal(targetX, targetY);

        // First we need to find the chunk path
        var chunkPath = GetChunkPath(startChunkX, startChunkY, targetChunkX, targetChunkY);
        if (chunkPath.Length == 0) return []; // No path found

        // Now we need to find the zone path
        Chunk? startChunk = GetChunk(chunkPath[0].chunkX, chunkPath[0].chunkY);
        if (startChunk == null) return []; // No path found
        Chunk? endChunk = null;
        if (chunkPath.Length == 1)
        {
            endChunk = startChunk;
        }
        else
        {
            endChunk = GetChunk(chunkPath[1].chunkX, chunkPath[1].chunkY);
        }

        var zonePath = GetZonePath(startLocalX, startLocalY, targetLocalX, targetLocalY, startChunk, endChunk);
        if (zonePath.Count == 0) return []; // No path found

        // Finally we need to find the tile path
        var tilePath = GetTilePath(startLocalX, startLocalY, targetLocalX, targetLocalY, zonePath);
        return ConvertToWorldCoordinates(tilePath, startChunkX, startChunkY);
    }

    private static (int x, int y)[] ConvertToWorldCoordinates((byte x, byte y)[] path, byte startChunkX, byte startChunkY)
    {
        var worldPath = new List<(int x, int y)>();
        foreach (var (x, y) in path)
        {
            worldPath.Add((x + startChunkX * Config.CHUNK_SIZE, y + startChunkY * Config.CHUNK_SIZE));
        }
        return [.. worldPath];
    }

    private static (byte x, byte y) GetNeighborFromExit(byte x, byte y, Direction dir)
    {
        return dir switch
        {
            Direction.Up => (x, (byte)(y - 1)),
            Direction.Right => ((byte)(x + 1), y),
            Direction.Down => (x, (byte)(y + 1)),
            Direction.Left => ((byte)(x - 1), y),
            _ => (x, y)
        };
    }


    private static (byte chunkX, byte chunkY)[] ReconstructPath(Dictionary<(byte x, byte y), (byte x, byte y)> cameFrom, (byte x, byte y) current)
    {
        var path = new List<(byte x, byte y)> { current };

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return [.. path];
    }

    public readonly record struct Zone
    {
        public required int Id { get; init; }
        public required (byte X, byte Y) ChunkPosition { get; init; }
        public required HashSet<(byte X, byte Y)> Tiles { get; init; }
        public required HashSet<(byte X, byte Y)> Borders { get; init; }
        public required HashSet<(int ZoneId, float Cost)> AdjacentZones { get; init; }
        public required HashSet<(Direction Dir, byte X, byte Y, float Cost)> Exits { get; init; }
    }

    internal static class ZoneManager
    {
        private static readonly ConcurrentDictionary<(byte X, byte Y), List<Zone>> _zonesByChunk = [];
        private static readonly ConcurrentDictionary<int, Zone> _zonesById = [];

        internal static void DetectZonesForChunk(Chunk chunk)
        {
            var visited = new bool[Config.CHUNK_SIZE, Config.CHUNK_SIZE];
            var zones = new List<Zone>();

            for (byte x = 0; x < Config.CHUNK_SIZE; x++)
            {
                for (byte y = 0; y < Config.CHUNK_SIZE; y++)
                {
                    if (!visited[x, y] && chunk.IsTraversable(x, y))
                    {
                        var zone = FloodFillNewZone(chunk, x, y, ref visited);
                        zones.Add(zone);
                        _zonesById[zone.Id] = zone;
                    }
                }
            }
            ConnectZonesInChunk(zones);
            _zonesByChunk[(chunk.X, chunk.Y)] = zones;
        }

        private static Zone FloodFillNewZone(Chunk chunk, byte startX, byte startY, ref bool[,] visited)
        {
            var tiles = new HashSet<(byte X, byte Y)>();
            var borders = new HashSet<(byte X, byte Y)>();
            var exits = new HashSet<(Direction Dir, byte X, byte Y, float Cost)>();
            var queue = new Queue<(byte X, byte Y)>();

            queue.Enqueue((startX, startY));

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                if (visited[x, y]) continue;

                visited[x, y] = true;
                tiles.Add((x, y));

                if (IsEdgeTile(x, y))
                {
                    var dir = GetDirectionFromEdge(x, y);
                    if (dir != Direction.None)
                    {
                        exits.Add((dir, x, y, 1.0f));
                    }
                }

                foreach (var (nx, ny) in GetNeighbors(chunk, x, y))
                {
                    if (!visited[nx, ny])
                    {
                        if (chunk.IsTraversable(nx, ny))
                        {
                            queue.Enqueue((nx, ny));
                        }
                        else
                        {
                            borders.Add((x, y));
                        }
                    }
                }
            }

            return new Zone
            {
                Id = Guid.NewGuid().GetHashCode(),
                ChunkPosition = (chunk.X, chunk.Y),
                Tiles = tiles,
                Borders = borders,
                AdjacentZones = [],
                Exits = exits
            };
        }

        private static void ConnectZonesInChunk(List<Zone> zones)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                for (int j = i + 1; j < zones.Count; j++)
                {
                    var zone1 = zones[i];
                    var zone2 = zones[j];

                    if (AreZonesAdjacent(zone1, zone2))
                    {
                        var cost = CalculateZoneTransitionCost(zone1, zone2);

                        // Create new zone instances with updated adjacency
                        zones[i] = zone1 with
                        {
                            AdjacentZones = [.. zone1.AdjacentZones, (zone2.Id, cost)]
                        };

                        zones[j] = zone2 with
                        {
                            AdjacentZones = [.. zone2.AdjacentZones, (zone1.Id, cost)]
                        };
                    }
                }
            }
        }

        private static bool AreZonesAdjacent(Zone a, Zone b)
        {
            return a.Borders.Any(borderA =>
                b.Borders.Any(borderB =>
                    Math.Abs(borderA.X - borderB.X) + Math.Abs(borderA.Y - borderB.Y) == 1
                )
            );
        }

        private static float CalculateZoneTransitionCost(Zone a, Zone b) => 1.0f;

        internal static Zone? GetZone(int zoneId) =>
            _zonesById.TryGetValue(zoneId, out var zone) ? zone : null;

        internal static List<Zone> GetZonesInChunk(byte chunkX, byte chunkY) =>
            _zonesByChunk.TryGetValue((chunkX, chunkY), out var zones) ? zones : [];

    }

    private const float NOISE_SCALE = 0.3f;  // Adjust this to change terrain feature size
    private const float WALKABLE_THRESHOLD = 0.2f;  // Higher = more walkable areas

    public static void GenerateWorld()
    {
        // Use a random offset for the noise to get different patterns each time
        Random rand = new();
        float offsetX = rand.Next(0, 1000);
        float offsetY = rand.Next(0, 1000);
        int chunkCounter = 0;
        // Generate each chunk and immediately build its zones
        for (byte chunkY = 0; chunkY < Config.WORLD_SIZE; chunkY++)
        {
            for (byte chunkX = 0; chunkX < Config.WORLD_SIZE; chunkX++)
            {
                chunkCounter++;
                System.Console.Clear();
                System.Console.WriteLine($"Generating chunk {chunkCounter} / {Config.WORLD_SIZE * Config.WORLD_SIZE}");
                // Generate basic chunk
                GenerateChunk(chunkX, chunkY, offsetX, offsetY);
            }
        }
    }

    private static void GenerateChunk(byte chunkX, byte chunkY, float offsetX, float offsetY)
    {
        var chunk = new Chunk(chunkX, chunkY);
        for (byte localY = 0; localY < Config.CHUNK_SIZE; localY++)
        {
            for (byte localX = 0; localX < Config.CHUNK_SIZE; localX++)
            {
                // Convert to world coordinates for continuous noise
                float worldX = chunkX * Config.CHUNK_SIZE + localX;
                float worldY = chunkY * Config.CHUNK_SIZE + localY;

                // Generate noise value
                float noiseValue = PerlinNoise.Generate(
                    (worldX + offsetX) * NOISE_SCALE,
                    (worldY + offsetY) * NOISE_SCALE
                );

                // Normalize noise value from [-1, 1] to [0, 1]
                noiseValue = (noiseValue + 1) * 0.5f;

                // Determine tile properties based on noise
                bool isTraversable = noiseValue > WALKABLE_THRESHOLD;
                bool isTransparent = true;  // Most tiles are transparent for now
                byte tileType = DetermineTileType(noiseValue);

                // Set base tile properties
                // byte tile = 0;
                chunk.SetTraversable(localX, localY, isTraversable);
                chunk.SetTransparent(localX, localY, isTransparent);
                chunk.SetTileType(localX, localY, tileType);
            }
        }
        BuildChunkExits(chunk);
        ZoneManager.DetectZonesForChunk(chunk);
        SetChunk(chunkX, chunkY, chunk);

    }

    private static byte DetermineTileType(float noiseValue)
    {
        // Convert noise value to tile type (0-3)
        if (noiseValue < 0.3f) return 0;        // Water/Obstacle
        else if (noiseValue < 0.6f) return 1;   // Grass/Path
        else if (noiseValue < 0.8f) return 2;   // Hills/Rough terrain
        else return 3;                          // Mountains/Special
    }

    public static string GetWorldInfo()
    {
        var sb = new StringBuilder();
        for (byte y = 0; y < Config.WORLD_SIZE; y++)
        {
            for (byte x = 0; x < Config.WORLD_SIZE; x++)
            {
                var chunk = GetChunk(x, y);
                if (chunk == null) continue;

                sb.AppendLine($"Chunk ({x}, {y}):");
            }
        }

        return sb.ToString();
    }
}