using System.Collections.Concurrent;
using System.Numerics;
using WebPeli.GameEngine.EntitySystem;
using WebPeli.GameEngine.Managers;

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
    private const float MIN_VIEWPORT_SIZE = 100;  // pixels
    private const float MAX_VIEWPORT_SIZE = 2000; // pixels
    public readonly record struct ChunkExit
    {
        public required byte X { get; init; }
        public required byte Y { get; init; }
        public required Direction Direction { get; init; }
    }

    public readonly record struct ChunkZone
    {
        public required int ZoneId { get; init; }
        public required HashSet<ChunkExit> Exits { get; init; }
        public required HashSet<(byte X, byte Y)> Tiles { get; init; }
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

    public static byte[,] GetTilesInArea(
        float screenX,
        float screenY,
        float viewportWidth,
        float viewportHeight,
        float? worldWidth = null,
        float? worldHeight = null)
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

    public static EntityGridCell[,] GetEntitiesInArea(
        float screenX,
        float screenY,
        float viewportWidth,
        float viewportHeight,
        float? worldWidth = null,
        float? worldHeight = null)
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

    public readonly record struct ZoneConnection
    {
        public required (int ChunkX, int ChunkY, int ZoneId) ZoneA { get; init; }
        public required (int ChunkX, int ChunkY, int ZoneId) ZoneB { get; init; }
        public required ChunkExit ExitA { get; init; }
        public required ChunkExit ExitB { get; init; }
        public required float Cost { get; init; }
    }

    public static readonly ConcurrentDictionary<(int ChunkX, int ChunkY), List<ChunkZone>> _chunkZones = [];
    public static readonly ConcurrentDictionary<((int X, int Y, int Zone), (int X, int Y, int Zone)), ZoneConnection> _zoneConnections = [];

    public static List<ChunkZone> FindChunkZones(Chunk chunk)
    {
        var zones = new List<ChunkZone>();
        var visited = new bool[Config.CHUNK_SIZE, Config.CHUNK_SIZE];

        for (byte x = 0; x < Config.CHUNK_SIZE; x++)
        {
            for (byte y = 0; y < Config.CHUNK_SIZE; y++)
            {
                if (!visited[x, y] && chunk.IsTraversable(x, y))
                {
                    var zone = FloodFillZone(chunk, x, y, ref visited);
                    zones.Add(zone);
                }
            }
        }
        return zones;
    }

    private static ChunkZone FloodFillZone(Chunk chunk, byte startX, byte startY, ref bool[,] visited)
    {
        var zoneId = Guid.NewGuid().GetHashCode();
        var zone = new ChunkZone
        {
            ZoneId = zoneId,
            Exits = [],
            Tiles = []
        };

        var queue = new Queue<(byte X, byte Y)>();
        queue.Enqueue((startX, startY));

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();

            if (visited[x, y]) continue;
            visited[x, y] = true;

            zone.Tiles.Add((x, y));

            // Check all neighbors
            foreach (var (nx, ny) in GetNeighbors(chunk, x, y))
            {
                if (!visited[nx, ny] && chunk.IsTraversable(nx, ny))
                {
                    queue.Enqueue((nx, ny));
                }
                else if (!chunk.IsTraversable(nx, ny))
                {
                    // Check if this is an exit tile
                    if (IsEdgeTile(nx, ny))
                    {
                        zone.Exits.Add(new ChunkExit { X = nx, Y = ny, Direction = GetDirectionFromEdge(nx, ny) });
                    }
                }
            }
        }
        return zone;
    }

    public static void ConnectZones()
    {
        for (byte ChunkY = 0; ChunkY < Config.WORLD_SIZE; ChunkY++)
        {
            for (byte ChunkX = 0; ChunkX < Config.WORLD_SIZE; ChunkX++)
            {
                var chunk = GetChunk(ChunkX, ChunkY);
                if (chunk == null) return;

                var zones = _chunkZones[(ChunkX, ChunkY)];
                foreach (var zone in zones)
                {
                    foreach (var exit in zone.Exits)
                    {
                        var (nx, ny) = GetNeighborChunk(ChunkX, ChunkY, exit.Direction);
                        var neighbor = GetChunk(nx, ny);
                        if (neighbor == null) continue;

                        var neighborZones = _chunkZones[(nx, ny)];
                        foreach (var neighborZone in neighborZones)
                        {
                            if (zone.ZoneId == neighborZone.ZoneId) continue;

                            var connection = new ZoneConnection
                            {
                                ZoneA = (ChunkX, ChunkY, zone.ZoneId),
                                ZoneB = (nx, ny, neighborZone.ZoneId),
                                ExitA = exit,
                                ExitB = neighborZone.Exits.First(),
                                Cost = Heuristic((exit.X, exit.Y), (neighborZone.Exits.First().X, neighborZone.Exits.First().Y))
                            };
                            _zoneConnections[((ChunkX, ChunkY, zone.ZoneId), (nx, ny, neighborZone.ZoneId))] = connection;
                        }
                    }
                }
            }
        }
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

    private static (int ZoneId, ChunkExit Exit)[] GetZonePath(int startX, int startY, Chunk startChunk, Chunk? targetChunk, Direction targetDirection)
    {
        var openSet = new PriorityQueue<(int ZoneId, ChunkExit? Exit), float>();
        var cameFrom = new Dictionary<int, (int ZoneId, ChunkExit? Exit)>();
        var gScore = new Dictionary<int, float>();

        // Get starting zone
        var (_, _, startLocalX, startLocalY) = Util.CoordinateSystem.WorldToChunkAndLocal(startX, startY);
        var startZone = _chunkZones[(startChunk.X, startChunk.Y)]
            .First(z => z.Tiles.Contains((startLocalX, startLocalY)));

        // Initialize search
        openSet.Enqueue((startZone.ZoneId, null), 0);
        gScore[startZone.ZoneId] = 0;

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            var currentZone = _chunkZones[(startChunk.X, startChunk.Y)]
                .First(z => z.ZoneId == current.ZoneId);

            // If we're pathing to next chunk, check if this zone has valid exit in target direction
            if (targetChunk != startChunk)
            {
                var validExit = currentZone.Exits
                    .FirstOrDefault(e => e.Direction == targetDirection);

                if (validExit != null)
                {
                    // We found path to next chunk!
                    return ReconstructZonePath(cameFrom, current.ZoneId);
                }
            }
            else
            {
                // Same chunk - check if we reached target zone
                // (You'll need to pass target coords to identify target zone)
                // ...
            }

            // Check connections to other zones
            foreach (var exit in currentZone.Exits)
            {
                // Get connecting zone through this exit
                var connection = _zoneConnections.FirstOrDefault(c =>
                    c.Key.Item1 == (startChunk.X, startChunk.Y, current.ZoneId));

                if (connection.Value == null) continue;

                var nextZone = connection.Value.ZoneB;
                var tentativeScore = gScore[current.ZoneId] + connection.Value.Cost;

                if (!gScore.ContainsKey(nextZone.ZoneId) ||
                    tentativeScore < gScore[nextZone.ZoneId])
                {
                    cameFrom[nextZone.ZoneId] = (current.ZoneId, exit);
                    gScore[nextZone.ZoneId] = tentativeScore;

                    // If pathing to next chunk, prioritize zones with exits in target direction
                    float heuristic = nextZone.Exits.Any(e => e.Direction == targetDirection) ? 0 : 1;
                    openSet.Enqueue((nextZone.ZoneId, exit), tentativeScore + heuristic);
                }
            }
        }

        return []; // No path found
    }

    private static (byte x, byte y)[] GetTilePath(byte startX, byte startY, byte targetX, byte targetY, ChunkZone startZone, ChunkZone endZone)
    {
        var openSet = new PriorityQueue<(byte X, byte Y), float>();
        var cameFrom = new Dictionary<(byte X, byte Y), (byte X, byte Y)>();
        var gScore = new Dictionary<(byte X, byte Y), float>();

        var (_, _, startLocalX, startLocalY) = Util.CoordinateSystem.WorldToChunkAndLocal(startX, startY);
        var (_, _, targetLocalX, targetLocalY) = Util.CoordinateSystem.WorldToChunkAndLocal(targetX, targetY);

        openSet.Enqueue((startLocalX, startLocalY), 0);
        gScore[(startLocalX, startLocalY)] = 0;

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current.X == targetLocalX && current.Y == targetLocalY)
            {
                return ReconstructPath(cameFrom, current);
            }

            // Get valid neighbors based on chunk exits
            if (!_chunkExits.TryGetValue((current.X, current.Y), out var exits))
                continue;

            foreach (var exit in exits)
            {
                // Determine neighbor chunk based on exit direction
                var (x, y) = GetNeighborFromExit(current.X, current.Y, exit.Direction);
                if (!IsInChunkBounds(x, y))
                    continue;

                var tentativeScore = gScore[(current.X, current.Y)] + 1;

                if (!gScore.ContainsKey((x, y)) ||
                    tentativeScore < gScore[(x, y)])
                {
                    cameFrom[(x, y)] = (current.X, current.Y);
                    gScore[(x, y)] = tentativeScore;
                    var priority = tentativeScore + Heuristic((x, y), (targetLocalX, targetLocalY));
                    openSet.Enqueue((x, y), priority);
                }
            }
        }

        return []; // No path found
    }



    public static (int x, int y)[] FindPath(int startX, int startY, int targetX, int targetY)
    {
        var (startChunkX, startChunkY, startLocalX, startLocalY) = Util.CoordinateSystem.WorldToChunkAndLocal(startX, startY);
        var (targetChunkX, targetChunkY, targetLocalX, targetLocalY) = Util.CoordinateSystem.WorldToChunkAndLocal(targetX, targetY);

        if (startChunkX == targetChunkX && startChunkY == targetChunkY)
        {
            // Same chunk, find path within the chunk
            var chunk = GetChunk(startChunkX, startChunkY);
            if (chunk == null)
                return []; // No path found




            return []; // Placeholder
        }

        var chunkPath = GetChunkPath(startChunkX, startChunkY, targetChunkX, targetChunkY);
        if (chunkPath.Length == 0)
            return []; // No path found

        // Now we need to find the zone path
        var startChunk = GetChunk(chunkPath[0].chunkX, chunkPath[0].chunkY);
        var endChunk = GetChunk(chunkPath[1].chunkX, chunkPath[1].chunkY);  // Assuming we have at least 2 chunks in the path

        if (startChunk == null || endChunk == null)
            return []; // No path found


        var zonePath = GetZonePath(startLocalX, startLocalY, targetLocalX, targetLocalY, startChunk, endChunk);
        if (zonePath.Length == 0)
            return []; // No path found

        // Now we need to find the path within the zone
        return []; // Placeholder
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







}