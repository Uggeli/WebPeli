using System.Collections.Concurrent;
using System.Text;
using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.WorldData;

namespace WebPeli.GameEngine;

public readonly struct LocalTilePos : IEquatable<LocalTilePos>
{
    public byte ChunkX { get; init; }
    public byte ChunkY { get; init; }
    public byte X { get; init; }
    public byte Y { get; init; }

    public bool Equals(LocalTilePos other) =>
        ChunkX == other.ChunkX &&
        ChunkY == other.ChunkY &&
        X == other.X &&
        Y == other.Y;

    public override bool Equals(object? obj) =>
        obj is LocalTilePos pos && Equals(pos);

    public override int GetHashCode() =>
        HashCode.Combine(ChunkX, ChunkY, X, Y);

    public static bool operator ==(LocalTilePos left, LocalTilePos right) =>
        left.Equals(right);

    public static bool operator !=(LocalTilePos left, LocalTilePos right) =>
        !left.Equals(right);
}

public readonly struct Position
{
    public int X { get; init; }  // World coordinates
    public int Y { get; init; }  // World coordinates
    public readonly (byte X, byte Y) ChunkPosition => (X: (byte)(X / Config.CHUNK_SIZE_BYTE), Y: (byte)(Y / Config.CHUNK_SIZE_BYTE));
    public readonly (byte X, byte Y) TilePosition => (X: (byte)(X % Config.CHUNK_SIZE_BYTE), Y: (byte)(Y % Config.CHUNK_SIZE_BYTE));
    public static Position operator +(Position a, Position b)
    {
        return new Position { X = a.X + b.X, Y = a.Y + b.Y };
    }
    public static Position operator +(Position a, (int X, int Y) b)
    {
        return new Position { X = a.X + b.X, Y = a.Y + b.Y };
    }
    public static Position operator -(Position a, Position b)
    {
        return new Position { X = a.X - b.X, Y = a.Y - b.Y };
    }
    public static Position operator -(Position a, (int X, int Y) b)
    {
        return new Position { X = a.X - b.X, Y = a.Y - b.Y };
    }
    public static bool operator ==(Position a, Position b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Position a, Position b) => a.X != b.X || a.Y != b.Y;
    public override bool Equals(object? obj)
    {
        return obj != null && obj is Position pos && X == pos.X && Y == pos.Y;
    }
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public Direction LookAt(Position target)
    {
        var dx = target.X - X;
        var dy = target.Y - Y;
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            return dx > 0 ? Direction.Right : Direction.Left;
        }
        return dy > 0 ? Direction.Down : Direction.Up;
    }
}

public enum CurrentAction : byte
{
    Idle,
    Moving,
    Attacking,
}

public class EntityState(Position[] position, CurrentAction currentAction, Direction direction, byte entityVolume = 200)
{
    public byte EntityVolume { get; set; } = entityVolume;  // 200 is default volume
    public Position[] Position { get; set; } = position;
    public CurrentAction CurrentAction { get; set; } = currentAction;
    public Direction Direction { get; set; } = direction;
}

public static class World
{

    // private static readonly int _worldGridSize = Config.WORLD_SIZE * Config.WORLD_SIZE;
    // private static readonly int _chunkSize = Config.CHUNK_SIZE_BYTE * Config.CHUNK_SIZE_BYTE;
    private static ConcurrentDictionary<(byte X, byte Y), Chunk> _chunks = [];
    private static ConcurrentDictionary<int, EntityState> _entityStates = [];
    private static Dictionary<(int x, int y), ChunkConnection> _chunkGraph = [];


    // Accessors, Map data
    public static (byte material, TileSurface surface, TileProperties properties) GetTileAt(Position pos)
    {
        var chunkPos = pos.ChunkPosition;
        if (!IsInWorldBounds(pos)) return (0, TileSurface.None, TileProperties.None);
        var (X, Y) = pos.TilePosition;
        if (!IsInChunkBounds(X, Y)) return (0, TileSurface.None, TileProperties.None);
        return _chunks[chunkPos].GetTile(X, Y);
    }

    public static void SetTileAt(Position pos, byte material, TileSurface surface, TileProperties properties)
    {
        var chunkPos = pos.ChunkPosition;
        var (X, Y) = pos.TilePosition;
        _chunks[chunkPos].SetTile(X, Y, material, surface, properties);
    }

    public static byte[,] GetTilesInArea(Position center, int width, int height)
    {
        var tiles = new byte[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var pos = center + (x - width / 2, y - height / 2);
                tiles[x, y] = GetTileAt(pos).material;
            }
        }
        return tiles;
    }

    public static byte[,] GetTilesInArea(float cameraX, float cameraY, float viewportWidth, float viewportHeight, float? worldWidth = null, float? worldHeight = null)
    {
        var width = (int)viewportWidth;
        var height = (int)viewportHeight;
        var center = new Position { X = (int)cameraX, Y = (int)cameraY };
        return GetTilesInArea(center, width, height);
    }

    private static Chunk? GetChunk((byte X, byte Y) pos)
    {
        return _chunks.TryGetValue(pos, out var c) ? c : null;
    }

    private static Chunk? GetChunk(Position pos)
    {
        return GetChunk(pos.ChunkPosition);
    }

    public static void SetChunk((byte X, byte Y) pos, Chunk chunk)
    {
        _chunks[pos] = chunk;
    }

    // Accessors, Entity data
    public static EntityState? GetEntityState(int entityId)
    {
        return _entityStates.TryGetValue(entityId, out var state) ? state : null;
    }

    public static void SetEntityState(int entityId, EntityState state)
    {
        _entityStates[entityId] = state;
    }

    public static void RemoveEntityState(int entityId)
    {
        _entityStates.TryRemove(entityId, out _);
    }

    public static void AddEntity(int id, EntityState? state = null)
    {
        if (_entityStates.ContainsKey(id)) return;

        if (state != null)
        {
            foreach (var pos in state.Position)  // Check if all positions are valid
            {
                var chunk = GetChunk(pos.ChunkPosition);
                if (chunk == null || !chunk.CanAddEntity(pos, state.EntityVolume)) return;
            }

            foreach (var pos in state.Position)
            {
                var chunk = GetChunk(pos.ChunkPosition);
                chunk?.AddEntity(id, pos, state.EntityVolume);
            }
            _entityStates[id] = state;
        }

        else
        {
            var spawnPoints = FindRandomSpawnPoint();
            if (!spawnPoints.Any()) return;

            var freshState = new EntityState([.. spawnPoints], CurrentAction.Idle, Direction.South);
            foreach (var pos in spawnPoints)
            {
                var chunk = GetChunk(pos.ChunkPosition);
                chunk?.AddEntity(id, pos, freshState.EntityVolume);
            }
            _entityStates[id] = freshState;
        }
    }

    private static List<Position> FindRandomSpawnPoint(byte entitySize = 1)
    {
        
        var chunk = GetChunk(((byte)Tools.Random.Next(Config.WORLD_SIZE), (byte)Tools.Random.Next(Config.WORLD_SIZE)));
        if (chunk == null) return [];

        byte attempts = 0;

        while (attempts < 10)
        {
            var x = (byte)Tools.Random.Next(Config.CHUNK_SIZE_BYTE);
            var y = (byte)Tools.Random.Next(Config.CHUNK_SIZE_BYTE);
            var pos = new Position { X = chunk.X * Config.CHUNK_SIZE_BYTE + x, Y = chunk.Y * Config.CHUNK_SIZE_BYTE + y };
            List<Position> positions = [];
            for (int dx = 0; dx < entitySize; dx++)
            {
                for (int dy = 0; dy < entitySize; dy++)
                {
                    var checkPos = pos + (dx, dy);
                    if (!IsInWorldBounds(checkPos) || !IsInChunkBounds(checkPos) || !GetTileAt(checkPos).properties.HasFlag(TileProperties.Walkable))
                    {
                        attempts++;
                        continue;
                    }
                    positions.Add(checkPos);
                }
            }

            if (positions.Count == entitySize * entitySize)
            {
                return positions;
            }
        }
        return [];
    }

    public static void RemoveEntity(int id)
    {
        if (!_entityStates.TryGetValue(id, out var state)) return;

        foreach (var pos in state.Position)
        {
            var chunk = GetChunk(pos.ChunkPosition);
            chunk?.RemoveEntity(id, pos, state.EntityVolume);
        }
        _entityStates.TryRemove(id, out _);
    }


    // Bounds checking
    public static bool IsInChunkBounds(byte X, byte Y) =>
        X >= 0 && X < Config.CHUNK_SIZE_BYTE && Y >= 0 && Y < Config.CHUNK_SIZE_BYTE;

    public static bool IsInChunkBounds(Position pos) =>
        IsInChunkBounds(pos.TilePosition.X, pos.TilePosition.Y);

    public static bool IsInWorldBounds(int X, int Y) =>
        X >= 0 && X < Config.WORLD_SIZE && Y >= 0 && Y < Config.WORLD_SIZE;

    public static bool IsInWorldBounds(Position pos) => IsInWorldBounds(pos.ChunkPosition.X, pos.ChunkPosition.Y);

    // Movement
    private static bool CanMoveTo(Position pos)
    {
        var (X, Y) = pos.TilePosition;
        return IsInChunkBounds(X, Y) && GetTileAt(pos).properties.HasFlag(TileProperties.Walkable);
    }

    private static (int X, int Y)[] GetChunkNeighbours(int x, int y)
    {
        var neighbours = new List<(int X, int Y)>();
        foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
        {
            var nx = x + dx;
            var ny = y + dy;
            if (IsInWorldBounds(nx, ny))
            {
                neighbours.Add((nx, ny));
            }
        }
        return [.. neighbours];
    }

    private static (int x, int y) GetNeighborPosition((int x, int y) pos, Direction direction)
    {
        var value = direction switch
        {
            Direction.North => (pos.x, pos.y - 1),
            Direction.East => (pos.x + 1, pos.y),
            Direction.South => (pos.x, pos.y + 1),
            Direction.West => (pos.x - 1, pos.y),
            _ => pos
        };
        return value;
    }

    private static List<Zone> GetZoneNeighbours(Position pos)
    {
        var (X, Y) = pos.ChunkPosition;
        var neighbours = new List<Zone>();
        // TODO
        return neighbours;
    }
    private static Position LocalToWorld(LocalTilePos local) => new()
    {
        X = (local.ChunkX * Config.CHUNK_SIZE_BYTE) + local.X,
        Y = (local.ChunkY * Config.CHUNK_SIZE_BYTE) + local.Y
    };

    private static LocalTilePos WorldToLocal(Position pos) => new()
    {
        ChunkX = (byte)(pos.X / Config.CHUNK_SIZE_BYTE),
        ChunkY = (byte)(pos.Y / Config.CHUNK_SIZE_BYTE),
        X = (byte)(pos.X % Config.CHUNK_SIZE_BYTE),
        Y = (byte)(pos.Y % Config.CHUNK_SIZE_BYTE)
    };

    public static Position[] GetPath(Position worldStart, Position worldEnd)
    {
        var localStart = WorldToLocal(worldStart);
        var localEnd = WorldToLocal(worldEnd);

        // Get first two chunks and new endpoint
        var (chunkPath, chunkEnd) = FindPathChunkLevel(localStart, localEnd);
        if (chunkPath.Length == 0) return [];

        // Get first two zones and refined endpoint
        var chunks = chunkPath.Take(2).ToArray();
        var (zonePath, zoneEnd) = FindZonePath(localStart, chunkEnd, chunks);
        if (zonePath.Length == 0) return [];

        // Final tile-level path
        var tilePath = FindTilePath(localStart, zoneEnd, zonePath);

        // Tilepath is in world coordinates
        return tilePath;
    }

    private static (LocalTilePos[], LocalTilePos) FindPathChunkLevel(LocalTilePos start, LocalTilePos end)
    {
        var path = new List<(byte X, byte Y)>();
        var current = (start.ChunkX, start.ChunkY);
        var target = (end.ChunkX, end.ChunkY);

        // If in same or adjacent chunks, that's our path
        if (Math.Abs(current.ChunkX - target.ChunkX) <= 1 &&
            Math.Abs(current.ChunkY - target.ChunkY) <= 1)
        {
            path.Add(current);
            if (current != target) path.Add(target);

            // End point is original if in same chunk, otherwise pick suitable point in second chunk
            var newEnd = current == target ? end : PickEndpointInChunk(target, end);
            return (path.Select(p => new LocalTilePos { ChunkX = p.X, ChunkY = p.Y }).ToArray(), newEnd);
        }

        // Otherwise do proper A* for chunks...
        // Returns first two chunks in path and endpoint in second chunk
        path = [.. ChunkLevelAStar(current, target)];
        if (path.Count == 0) return ([], default);

        var firstTwoChunks = path.Take(2).ToArray();
        var endChunk = firstTwoChunks[1];

        var newEndpoint = PickEndpointInChunk(endChunk, end);

        return (firstTwoChunks.Select(p => new LocalTilePos { ChunkX = p.X, ChunkY = p.Y }).ToArray(),
                newEndpoint);
    }

    public static (Zone[], LocalTilePos) FindZonePath(LocalTilePos start, LocalTilePos chunkEnd,
                                                      LocalTilePos[] chunks)
    {
        Chunk? startChunk = GetChunk((chunks[0].ChunkX, chunks[0].ChunkY));
        Chunk? endChunk;
        if (chunks.Length == 1)
            endChunk = GetChunk((chunks[0].ChunkX, chunks[0].ChunkY));
        else
            endChunk = GetChunk((chunks[1].ChunkX, chunks[1].ChunkY));
        if (startChunk == null || endChunk == null)
            return ([], default);

        // Find start zone
        var zones = startChunk.GetZones();
        if (!zones.Any()) return ([], default);

        var startZone = zones.First(z => z.TilePositions.Contains((start.X, start.Y)));

        // Look for valid path through zones using edges
        var connection = _chunkGraph[(startChunk.X, startChunk.Y)];
        var direction = GetConnectionDirection(startChunk, endChunk);

        // Get zones that can reach the boundary in the right direction
        var endZones = startZone.Edges.Values
            .Where(e => HasMatchingEdge(e, direction))
            .SelectMany(e => GetConnectedZones(endChunk, GetOppositeEdge(direction)))
            .Distinct()
            .ToList();

        if (!endZones.Any()) return ([], default);

        // Pick closest end zone and suitable endpoint in it
        var endZone = PickBestEndZone(endZones, chunkEnd);
        var newEnd = PickEndpointInZone(endZone, chunkEnd);

        return (new[] { startZone, endZone }, newEnd);
    }

    private static Position[] FindTilePath(LocalTilePos start, LocalTilePos end, Zone[] zones)
    {
        // Create search space from both zones
        var searchSpace = new HashSet<Position>();
        foreach (var zone in zones)
        {
            foreach (var (x, y) in zone.TilePositions)
            {
                searchSpace.Add(new Position
                {
                    X = (zone.ChunkPosition.X * Config.CHUNK_SIZE_BYTE) + x,
                    Y = (zone.ChunkPosition.Y * Config.CHUNK_SIZE_BYTE) + y
                });
            }
        }

        // Simple A* through the combined space
        var openSet = new PriorityQueue<Position, float>();
        var closedSet = new HashSet<Position>();
        var cameFrom = new Dictionary<Position, Position>();
        var gScore = new Dictionary<Position, float>();

        var worldStart = LocalToWorld(start);
        var worldEnd = LocalToWorld(end);

        if (!searchSpace.Contains(worldStart) || !searchSpace.Contains(worldEnd))
            return [];

        openSet.Enqueue(worldStart, 0);
        gScore[worldStart] = 0;

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            if (current == worldEnd)
            {
                return ReconstructPath(cameFrom, current);
            }

            closedSet.Add(current);

            // Check neighbors (just cardinal directions for now)
            foreach (var delta in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var next = new Position
                {
                    X = current.X + delta.Item1,
                    Y = current.Y + delta.Item2
                };

                if (!searchSpace.Contains(next) || closedSet.Contains(next))
                    continue;

                var tentativeG = gScore[current] + 1;

                if (!gScore.ContainsKey(next) || tentativeG < gScore[next])
                {
                    cameFrom[next] = current;
                    gScore[next] = tentativeG;
                    var h = Math.Abs(worldEnd.X - next.X) + Math.Abs(worldEnd.Y - next.Y);
                    openSet.Enqueue(next, tentativeG + h);
                }
            }
        }

        return [];
    }

    // Helper for finding best endpoint in a chunk based on direction we're heading
    private static LocalTilePos PickEndpointInChunk((byte X, byte Y) chunk, LocalTilePos target)
    {
        // For now just pick middle of chunk - could be smarter based on target direction
        return new LocalTilePos
        {
            ChunkX = chunk.X,
            ChunkY = chunk.Y,
            X = Config.CHUNK_SIZE_BYTE / 2,
            Y = Config.CHUNK_SIZE_BYTE / 2
        };
    }

    private static LocalTilePos PickEndpointInZone(Zone zone, LocalTilePos target)
    {
        // For now pick center of zone - could be smarter
        var center = zone.TilePositions.Aggregate(
            new { sumX = 0, sumY = 0, count = 0 },
            (acc, pos) => new
            {
                sumX = acc.sumX + pos.X,
                sumY = acc.sumY + pos.Y,
                count = acc.count + 1
            });

        return new LocalTilePos
        {
            ChunkX = zone.ChunkPosition.X,
            ChunkY = zone.ChunkPosition.Y,
            X = (byte)(center.sumX / center.count),
            Y = (byte)(center.sumY / center.count)
        };
    }

    private static Zone PickBestEndZone(List<Zone> possibleZones, LocalTilePos target)
    {
        // For now just pick first one - could be smarter based on distance to target
        return possibleZones[0];
    }

    private static Direction GetConnectionDirection(LocalTilePos from, LocalTilePos to)
    {
        if (to.ChunkX > from.ChunkX) return Direction.Right;
        if (to.ChunkX < from.ChunkX) return Direction.Left;
        if (to.ChunkY > from.ChunkY) return Direction.Down;
        return Direction.Up;
    }

    private static Direction GetConnectionDirection(Chunk from, Chunk to)
    {
        if (to.X > from.X) return Direction.Right;
        if (to.X < from.X) return Direction.Left;
        if (to.Y > from.Y) return Direction.Down;
        return Direction.Up;
    }



    private static bool HasMatchingEdge(ZoneEdge edge, Direction direction) =>
        direction switch
        {
            Direction.Right => edge.HasFlag(ZoneEdge.ChunkEast),
            Direction.Left => edge.HasFlag(ZoneEdge.ChunkWest),
            Direction.Down => edge.HasFlag(ZoneEdge.ChunkSouth),
            Direction.Up => edge.HasFlag(ZoneEdge.ChunkNorth),
            _ => false
        };

    private static IEnumerable<Zone> GetConnectedZones(Chunk chunk, ZoneEdge edge)
    {
        return chunk.GetZones().Where(z =>
            z.Edges.Values.Any(e => e.HasFlag(edge)));
    }

    private static ZoneEdge GetOppositeEdge(Direction direction) =>
        direction switch
        {
            Direction.Right => ZoneEdge.ChunkWest,
            Direction.Left => ZoneEdge.ChunkEast,
            Direction.Down => ZoneEdge.ChunkNorth,
            Direction.Up => ZoneEdge.ChunkSouth,
            _ => ZoneEdge.None
        };

    private static (byte X, byte Y)[] ChunkLevelAStar((byte, byte) start, (byte, byte) end)
    {
        var startChunk = GetChunk(start);
        var endChunk = GetChunk(end);
        if (startChunk == null || endChunk == null)
        {
            System.Console.WriteLine("Start or end chunk not found");
            return [];
        } 
            

        Queue<(int X, int Y)> openSet = new();
        HashSet<(int X, int Y)> closedSet = [];
        Dictionary<(int X, int Y), (int X, int Y)> cameFrom = [];

        openSet.Enqueue((startChunk.X, startChunk.Y));
        var endPos = (endChunk.X, endChunk.Y);

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            if (current == endPos) return ReconstructChunkPath(cameFrom, current);

            foreach (var neighbour in GetChunkNeighbours(current.X, current.Y))
            {
                if (closedSet.Contains(neighbour)) continue;

                var delta = (dx: current.X - neighbour.X, dy: current.Y - neighbour.Y);
                var connection = _chunkGraph[neighbour];

                var canMove = (delta, connection) switch
                {
                    ((1, 0) or (-1, 0), var c) when c.HasFlag(ChunkConnection.EastWest) => true,
                    ((0, 1) or (0, -1), var c) when c.HasFlag(ChunkConnection.NorthSouth) => true,
                    _ => false
                };

                if (!canMove) continue;

                openSet.Enqueue(neighbour);
                closedSet.Add(neighbour);
                cameFrom[neighbour] = current;
            }
        }
        return [];
    }

    private static (byte X, byte Y)[] ReconstructChunkPath(Dictionary<(int X, int Y), (int X, int Y)> cameFrom, (int X, int Y) current)
    {
        var path = new List<(byte X, byte Y)>();
        while (cameFrom.TryGetValue(current, out var previous))
        {
            path.Add(((byte X, byte Y))(current.X, current.Y));
            current = previous;
        }
        path.Reverse();
        if (path.Count == 1) return [.. path];
        return path.Take(2).ToArray();
    }

    private static Position[] ReconstructPath(Dictionary<Position, Position> cameFrom, Position current)
    {
        var path = new List<Position> { current };
        while (cameFrom.TryGetValue(current, out var previous))
        {
            path.Add(previous);
            current = previous;
        }
        path.Reverse();
        return path.ToArray();
    }



    // World generation
    public static void GenerateWorld()
    {
        WorldGenerator.GenerateWorld();
    }
    public static class WorldGenerator
    {
        // Core elevation thresholds for basic terrain types
        private static readonly float WATER_THRESHOLD = -0.3f;
        private static readonly float BEACH_THRESHOLD = -0.2f;
        private static readonly float MOUNTAIN_THRESHOLD = 0.5f;
        private static readonly float PEAK_THRESHOLD = 0.7f;

        public static void GenerateWorld()
        {
            GenerateChunks();
            DumbMapdataToFile();
            // GenerateAndCompare();
            // TestChunkAccess();
        }
        // Test method to expose the issue
        private static void TestChunkAccess()
        {
            var dummyChunk = new DummyChunk(0, 0);
            var realChunk = new Chunk(0, 0);

            // Set a test pattern using actual CHUNK_SIZE
            for (byte x = 0; x < Config.CHUNK_SIZE_BYTE; x++)
            {
                for (byte y = 0; y < Config.CHUNK_SIZE_BYTE; y++)
                {
                    // Let's use a simpler pattern that will make issues obvious
                    byte value = (byte)((x + y) % 4); // Or any other pattern that's easy to spot
                    dummyChunk.SetTile(x, y, value);
                    realChunk.SetTile(x, y, value, TileSurface.None, TileProperties.None);
                }
            }

            // Check a few key positions, including edges
            byte[][] positionsToCheck =
            [
                [0, 0],
                [(byte)(Config.CHUNK_SIZE_BYTE-1), 0],
                [0, (byte)(Config.CHUNK_SIZE_BYTE-1)],
                [(byte)(Config.CHUNK_SIZE_BYTE-1), (byte)(Config.CHUNK_SIZE_BYTE-1)],
                // [64, 64],  // Middle-ish
                // [32, 96],  // Random positions
                // [96, 32]
            ];

            Console.WriteLine("Checking key positions:");
            foreach (var pos in positionsToCheck)
            {
                byte x = pos[0], y = pos[1];
                byte dummyValue = dummyChunk.GetTile(x, y);
                byte realValue = realChunk.GetTile(x, y).material;
                Console.WriteLine($"Position ({x,3},{y,3}): Dummy={dummyValue,3} Real={realValue,3}" +
                                (dummyValue == realValue ? "" : " MISMATCH!"));
            }

            // Let's also check the actual 1D index calculation
            Console.WriteLine("\nChecking 1D index calculations for these positions:");
            foreach (var pos in positionsToCheck)
            {
                byte x = pos[0], y = pos[1];
                int index = x * Config.CHUNK_SIZE_BYTE + y;
                Console.WriteLine($"Position ({x,3},{y,3}) -> 1D index: {index,5}" +
                                (index < Config.CHUNK_SIZE_BYTE * Config.CHUNK_SIZE_BYTE ? "" : " OVERFLOW!"));
            }

            // Add this to the test
            Console.WriteLine("\nVisual 4x4 section of arrays:");
            Console.WriteLine("Dummy:");
            for (byte y = 0; y < 4; y++)
            {
                for (byte x = 0; x < 4; x++)
                {
                    Console.Write($"{dummyChunk.GetTile(x, y),3} ");
                }
                Console.WriteLine();
            }

            Console.WriteLine("\nReal:");
            for (byte y = 0; y < 4; y++)
            {
                for (byte x = 0; x < 4; x++)
                {
                    Console.Write($"{realChunk.GetTile(x, y).material,3} ");
                }
                Console.WriteLine();
            }
        }

        // Dummy chunk class just for testing
        private class DummyChunk
        {
            public byte[,] MapData { get; } = new byte[Config.CHUNK_SIZE_BYTE, Config.CHUNK_SIZE_BYTE];
            public byte X { get; }
            public byte Y { get; }

            public DummyChunk(byte x, byte y)
            {
                X = x;
                Y = y;
            }

            public void SetTile(byte x, byte y, byte material) => MapData[x, y] = material;
            public byte GetTile(byte x, byte y) => MapData[x, y];
        }

        private static void GenerateAndCompare()
        {
            Console.WriteLine("Generating comparison data...");
            float baseScale = 0.05f; // Adjust scale for terrain size

            var dummyChunks = new Dictionary<(byte X, byte Y), DummyChunk>();

            // Generate dummy chunks alongside real ones
            for (byte x = 0; x < Config.WORLD_SIZE; x++)
            {
                for (byte y = 0; y < Config.WORLD_SIZE; y++)
                {
                    var dummyChunk = new DummyChunk(x, y);
                    var realChunk = new Chunk(x, y);

                    // Generate terrain data for both
                    for (byte localX = 0; localX < Config.CHUNK_SIZE_BYTE; localX++)
                    {
                        for (byte localY = 0; localY < Config.CHUNK_SIZE_BYTE; localY++)
                        {
                            float worldX = x * Config.CHUNK_SIZE_BYTE + localX;
                            float worldY = y * Config.CHUNK_SIZE_BYTE + localY;

                            // Generate terrain value
                            float elevation = EnhancedPerlinNoise.GenerateTerrain(worldX * baseScale, worldY * baseScale);
                            var (material, _) = DetermineTileType(elevation);

                            // Set in both chunks
                            dummyChunk.SetTile(localX, localY, material);
                            realChunk.SetTile(localX, localY, material, TileSurface.None, TileProperties.None);
                        }
                    }

                    dummyChunks[(x, y)] = dummyChunk;
                    _chunks[(x, y)] = realChunk;
                }
            }

            // Write both to files for comparison
            using (var dummyWriter = new StreamWriter("mapdata_dummy.txt", false))
            using (var realWriter = new StreamWriter("mapdata_real.txt", false))
            {
                for (byte worldY = 0; worldY < Config.WORLD_SIZE; worldY++)
                {
                    for (byte chunkTileY = 0; chunkTileY < Config.CHUNK_SIZE_BYTE; chunkTileY++)
                    {
                        for (byte worldX = 0; worldX < Config.WORLD_SIZE; worldX++)
                        {
                            var dummyChunk = dummyChunks[(worldX, worldY)];
                            var realChunk = _chunks[(worldX, worldY)];

                            // Write dummy chunk data
                            for (byte chunkTileX = 0; chunkTileX < Config.CHUNK_SIZE_BYTE; chunkTileX++)
                            {
                                dummyWriter.Write(dummyChunk.GetTile(chunkTileX, chunkTileY));
                            }
                            dummyWriter.Write(" ");

                            // Write real chunk data
                            for (byte chunkTileX = 0; chunkTileX < Config.CHUNK_SIZE_BYTE; chunkTileX++)
                            {
                                realWriter.Write(realChunk.GetTile(chunkTileX, chunkTileY).material);
                            }
                            realWriter.Write(" ");
                        }
                        dummyWriter.WriteLine();
                        realWriter.WriteLine();
                    }
                    dummyWriter.WriteLine();
                    realWriter.WriteLine();
                }
            }
        }

        private static void DumbMapdataToFile()
        {
            System.Console.WriteLine("Writing map data to file...");

            if (File.Exists("mapdata.txt"))
            {
                File.Delete("mapdata.txt");
            }

            using var writer = new StreamWriter("mapdata.txt", false);

            // Write world row by row
            for (byte worldY = 0; worldY < Config.WORLD_SIZE; worldY++)
            {
                // For each row of chunks, we need to write CHUNK_SIZE lines
                for (byte chunkTileY = 0; chunkTileY < Config.CHUNK_SIZE_BYTE; chunkTileY++)
                {
                    // Write all chunks in this world row
                    for (byte worldX = 0; worldX < Config.WORLD_SIZE; worldX++)
                    {
                        var chunk = _chunks[(worldX, worldY)];

                        // Write one row of this chunk
                        for (byte chunkTileX = 0; chunkTileX < Config.CHUNK_SIZE_BYTE; chunkTileX++)
                        {
                            writer.Write(chunk.GetTile(chunkTileX, chunkTileY).material);
                        }
                        writer.Write(" "); // Separate chunks horizontally
                    }
                    writer.WriteLine(); // End of row
                }
                writer.WriteLine(); // Separate chunks vertically
            }
        }

        private static void GenerateChunks()
        {
            for (byte worldX = 0; worldX < Config.WORLD_SIZE; worldX++)
            {
                for (byte worldY = 0; worldY < Config.WORLD_SIZE; worldY++)
                {
                    Chunk newChunk = new(worldX, worldY);
                    GenerateChunkTerrain(newChunk);
                    ZoneManager.CreateZones(newChunk);
                    _chunks[(worldX, worldY)] = newChunk;
                }
            }

            BuildChunkGraph();
        }

        private static void BuildChunkGraph()
        {

            for (byte worldX = 0; worldX < Config.WORLD_SIZE; worldX++)
            {
                for (byte worldY = 0; worldY < Config.WORLD_SIZE; worldY++)
                {
                    var chunk = GetChunk((worldX, worldY));
                    if (chunk == null) continue;

                    var chunkZones = ZoneManager.GetZones(chunk);
                    var connections = ChunkConnection.None;

                    // First collect all edge tiles per direction
                    List<(byte x, byte y)> northEdgeTiles = [];
                    List<(byte x, byte y)> eastEdgeTiles = [];
                    List<(byte x, byte y)> southEdgeTiles = [];
                    List<(byte x, byte y)> westEdgeTiles = [];

                    foreach (var zone in chunkZones)
                    {
                        foreach (var (pos, edge) in zone.Edges)
                        {
                            if (edge.HasFlag(ZoneEdge.ChunkNorth)) northEdgeTiles.Add(pos);
                            if (edge.HasFlag(ZoneEdge.ChunkEast)) eastEdgeTiles.Add(pos);
                            if (edge.HasFlag(ZoneEdge.ChunkSouth)) southEdgeTiles.Add(pos);
                            if (edge.HasFlag(ZoneEdge.ChunkWest)) westEdgeTiles.Add(pos);
                        }
                    }

                    // Now check for valid connections with neighbor chunks
                    // North neighbor
                    if (worldY > 0 && northEdgeTiles.Count > 0)
                    {
                        var northChunk = GetChunk((worldX, (byte)(worldY - 1)));
                        if (northChunk != null && HasMatchingEdges(chunk, northChunk, northEdgeTiles, Direction.North))
                        {
                            // Check what other directions we can reach from these northern tiles
                            if (CanReachEdge(chunk, northEdgeTiles, ZoneEdge.ChunkEast))
                                connections |= ChunkConnection.NorthEast;
                            if (CanReachEdge(chunk, northEdgeTiles, ZoneEdge.ChunkWest))
                                connections |= ChunkConnection.NorthWest;
                            if (CanReachEdge(chunk, northEdgeTiles, ZoneEdge.ChunkSouth))
                                connections |= ChunkConnection.NorthSouth;
                        }
                    }

                    // South neighbor
                    if (worldY < Config.WORLD_SIZE - 1 && southEdgeTiles.Count > 0)
                    {
                        var southChunk = GetChunk((worldX, (byte)(worldY + 1)));
                        if (southChunk != null && HasMatchingEdges(chunk, southChunk, southEdgeTiles, Direction.South))
                        {
                            // Check what other directions we can reach from these southern tiles
                            if (CanReachEdge(chunk, southEdgeTiles, ZoneEdge.ChunkEast))
                                connections |= ChunkConnection.SouthEast;
                            if (CanReachEdge(chunk, southEdgeTiles, ZoneEdge.ChunkWest))
                                connections |= ChunkConnection.SouthWest;
                        }
                    }

                    // East neighbor
                    if (worldX < Config.WORLD_SIZE - 1 && eastEdgeTiles.Count > 0)
                    {
                        var eastChunk = GetChunk(((byte)(worldX + 1), worldY));
                        if (eastChunk != null && HasMatchingEdges(chunk, eastChunk, eastEdgeTiles, Direction.East))
                        {
                            // Check what other directions we can reach from these eastern tiles
                            if (CanReachEdge(chunk, eastEdgeTiles, ZoneEdge.ChunkWest))
                                connections |= ChunkConnection.EastWest;
                        }
                    }

                    _chunkGraph[(worldX, worldY)] = connections;
                }
            }
        }

        // Helper to check if tiles on one edge can reach another edge through zones
        private static bool CanReachEdge(Chunk chunk, List<(byte x, byte y)> startTiles, ZoneEdge targetEdge)
        {
            foreach (var zone in chunk.GetZones())
            {
                // If this zone contains any of our start tiles and has the target edge type
                if (zone.TilePositions.Intersect(startTiles).Any() &&
                    zone.Edges.Any(e => e.Value.HasFlag(targetEdge)))
                {
                    return true;
                }
            }
            return false;
        }

        // Helper to check if edge tiles match up between chunks
        private static bool HasMatchingEdges(Chunk chunk1, Chunk chunk2, List<(byte x, byte y)> edgeTiles, Direction direction)
        {
            // Convert edge tiles to corresponding positions in neighbor chunk
            var neighborPositions = edgeTiles.Select(pos => GetNeighborPosition(pos, direction));

            // Check if any of these positions are walkable in both chunks
            return neighborPositions.Any(pos =>
                TileManager.IsWalkable(chunk1.GetTile(pos.x, pos.y).properties) &&
                TileManager.IsWalkable(chunk2.GetTile(pos.x, pos.y).properties));
        }

        private static void GenerateChunkTerrain(Chunk chunk)
        {

            float baseScale = 0.05f; // Adjust scale for terrain size

            for (byte localX = 0; localX < Config.CHUNK_SIZE; localX++)
            {
                for (byte localY = 0; localY < Config.CHUNK_SIZE; localY++)
                {
                    float worldX = chunk.X * Config.CHUNK_SIZE + localX;
                    float worldY = chunk.Y * Config.CHUNK_SIZE + localY;

                    // Generate base terrain elevation
                    float elevation = EnhancedPerlinNoise.GenerateTerrain(worldX * baseScale, worldY * baseScale);
                    var (material, properties) = DetermineTileType(elevation);
                    chunk.SetTile(localX, localY, material, TileSurface.None, properties);
                }
            }

        }

        private static (byte material, TileProperties properties) DetermineTileType(float elevation)
        {
            TileMaterial material;
            TileProperties properties;

            if (elevation < WATER_THRESHOLD)
            {
                // Deep water
                material = TileMaterial.Water;
                properties = TileProperties.Transparent | TileProperties.BlocksProjectiles;
            }
            else if (elevation < BEACH_THRESHOLD)
            {
                // Beach/Shore
                material = TileMaterial.Sand;
                properties = TileProperties.Walkable | TileProperties.Breakable;
            }
            else if (elevation < MOUNTAIN_THRESHOLD)
            {
                // Regular terrain
                material = TileMaterial.Dirt;
                properties = TileProperties.Walkable | TileProperties.Solid | TileProperties.Breakable;
            }
            else if (elevation < PEAK_THRESHOLD)
            {
                // Mountains
                material = TileMaterial.Stone;
                properties = TileProperties.Solid | TileProperties.BlocksLight | TileProperties.Breakable;
            }
            else
            {
                // High peaks
                material = TileMaterial.Stone;
                properties = TileProperties.Solid | TileProperties.BlocksLight | TileProperties.BlocksProjectiles;
            }

            return ((byte)material, properties);
        }

        public static void DrawChunk(Chunk chunk)
        {
            for (byte y = 0; y < Config.CHUNK_SIZE_BYTE; y++)
            {
                for (byte x = 0; x < Config.CHUNK_SIZE_BYTE; x++)
                {
                    var (material, _, properties) = chunk.GetTile(x, y);
                    var glyph = material switch
                    {
                        (byte)TileMaterial.Water => '~',
                        (byte)TileMaterial.Sand => '.',
                        (byte)TileMaterial.Dirt => ',',
                        (byte)TileMaterial.Stone => '#',
                        _ => '?'
                    };
                    if (!properties.HasFlag(TileProperties.Walkable))
                    {
                        glyph = 'X';
                    }

                    Console.Write(glyph);
                    
                }
                Console.WriteLine();
            }
        }
    }
}


