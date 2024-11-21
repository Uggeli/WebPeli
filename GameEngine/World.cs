using System.Collections.Concurrent;
using System.Text;
using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.WorldData;

namespace WebPeli.GameEngine;

public readonly struct Position
{
    public int X { get; init; }
    public int Y { get; init; }
    public readonly (byte X, byte Y) ChunkPosition => (X: (byte)(X / Config.CHUNK_SIZE), Y: (byte)(Y / Config.CHUNK_SIZE));
    public readonly (byte X, byte Y) TilePosition => (X: (byte)(X % Config.CHUNK_SIZE), Y: (byte)(Y % Config.CHUNK_SIZE));
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
}

public enum CurrentAction : byte
{
    Idle,
    Moving,
    Attacking,
}

public class EntityState(Position position, CurrentAction currentAction, Direction direction)
{
    public Position Position { get; set; } = position;
    public CurrentAction CurrentAction { get; set; } = currentAction;
    public Direction Direction { get; set; } = direction;
}

public static class World
{
    private static readonly int _worldGridSize = Config.WORLD_SIZE * Config.WORLD_SIZE;
    private static readonly int _chunkSize = Config.CHUNK_SIZE * Config.CHUNK_SIZE;
    private static ConcurrentDictionary<(byte X, byte Y), Chunk> _chunks = [];
    private static ConcurrentDictionary<Guid, EntityState> _entityStates = [];
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

    // Accessors, Entity data
    public static EntityState? GetEntityState(Guid entityId)
    {
        return _entityStates.TryGetValue(entityId, out var state) ? state : null;
    }

    public static void SetEntityState(Guid entityId, EntityState state)
    {
        _entityStates[entityId] = state;
    }

    public static void RemoveEntityState(Guid entityId)
    {
        _entityStates.TryRemove(entityId, out _);
    }

    public static void AddEntity(Guid id)
    {
        // TODO
    }

    public static void RemoveEntity(Guid id)
    {
        // TODO
    }


    // Bounds checking
    public static bool IsInChunkBounds(byte X, byte Y) =>
        X >= 0 && X < Config.CHUNK_SIZE && Y >= 0 && Y < Config.CHUNK_SIZE;

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

    private static (byte x, byte y) GetNeighborPosition((byte x, byte y) pos, Direction direction)
    {
        return direction switch
        {
            Direction.North => (pos.x, (byte)(pos.y - 1)),
            Direction.East => ((byte)(pos.x + 1), pos.y),
            Direction.South => (pos.x, (byte)(pos.y + 1)),
            Direction.West => ((byte)(pos.x - 1), pos.y),
            _ => pos
        };
    }

    private static List<Zone> GetZoneNeighbours(Position pos)
    {
        var (X, Y) = pos.ChunkPosition;
        var neighbours = new List<Zone>();
        // TODO
        return neighbours;
    }

    public static Position[] GetPath(Position start, Position end)
    {
        if (start == end) return [];  // Already there
        // 1. find the chunk level path, ie can we path from start to end on chunk level if not return [], if we can return zones along the path, limiting to first and second zone to limit search space
        var chunkPath = FindPathChunkLevel(start, end);  // returns (x, y) positions for chunks
        if (chunkPath.Length == 0) return [];  // No path

        // 2. find zones in the chunks along the path
        var zones = FindSearchSpace(start, end, chunkPath);  // return start zone and the next zone along the path, limiting search space

        // 3. find the tile level path in the zones







        return [];
    }

    private static (byte X, byte Y)[] FindPathChunkLevel(Position start, Position end)
    {
        var startChunk = GetChunk(start.ChunkPosition);
        var endChunk = GetChunk(end.ChunkPosition);
        if (startChunk == null || endChunk == null) return [];

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

    private static (Zone start, Zone end)? FindSearchSpace(Position start, Position end, (byte X, byte Y)[] chunkPath)
    {
        return null;
    }

    private static (int x, int Y)[]? FindPathTileLevel(Position start, Position end)
    {
        return null;
    }

    private static List<Position> GetTileNeighbours(Position pos, bool includeOutsideChunk = false)
    {
        var (X, Y) = pos.TilePosition;
        var neighbours = new List<Position>();
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                var neighbour = pos + new Position { X = x, Y = y };
                if (IsInChunkBounds(neighbour))
                {
                    neighbours.Add(neighbour);
                }
                else if (includeOutsideChunk && IsInWorldBounds(neighbour.X * Config.CHUNK_SIZE, neighbour.Y * Config.CHUNK_SIZE))
                {
                    neighbours.Add(neighbour);
                }
            }
        }
        return neighbours;
    }




    // World generation
    public static void GenerateWorld()
    {
        WorldGenerator.GenerateWorld();
    }
    private static class WorldGenerator
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
            for (byte x = 0; x < Config.CHUNK_SIZE; x++)
            {
                for (byte y = 0; y < Config.CHUNK_SIZE; y++)
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
                [(byte)(Config.CHUNK_SIZE-1), 0],
                [0, (byte)(Config.CHUNK_SIZE-1)],
                [(byte)(Config.CHUNK_SIZE-1), (byte)(Config.CHUNK_SIZE-1)],
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
                int index = x * Config.CHUNK_SIZE + y;
                Console.WriteLine($"Position ({x,3},{y,3}) -> 1D index: {index,5}" +
                                (index < Config.CHUNK_SIZE * Config.CHUNK_SIZE ? "" : " OVERFLOW!"));
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
            public byte[,] MapData { get; } = new byte[Config.CHUNK_SIZE, Config.CHUNK_SIZE];
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
                    for (byte localX = 0; localX < Config.CHUNK_SIZE; localX++)
                    {
                        for (byte localY = 0; localY < Config.CHUNK_SIZE; localY++)
                        {
                            float worldX = x * Config.CHUNK_SIZE + localX;
                            float worldY = y * Config.CHUNK_SIZE + localY;

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
                    for (byte chunkTileY = 0; chunkTileY < Config.CHUNK_SIZE; chunkTileY++)
                    {
                        for (byte worldX = 0; worldX < Config.WORLD_SIZE; worldX++)
                        {
                            var dummyChunk = dummyChunks[(worldX, worldY)];
                            var realChunk = _chunks[(worldX, worldY)];

                            // Write dummy chunk data
                            for (byte chunkTileX = 0; chunkTileX < Config.CHUNK_SIZE; chunkTileX++)
                            {
                                dummyWriter.Write(dummyChunk.GetTile(chunkTileX, chunkTileY));
                            }
                            dummyWriter.Write(" ");

                            // Write real chunk data
                            for (byte chunkTileX = 0; chunkTileX < Config.CHUNK_SIZE; chunkTileX++)
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
                for (byte chunkTileY = 0; chunkTileY < Config.CHUNK_SIZE; chunkTileY++)
                {
                    // Write all chunks in this world row
                    for (byte worldX = 0; worldX < Config.WORLD_SIZE; worldX++)
                    {
                        var chunk = _chunks[(worldX, worldY)];

                        // Write one row of this chunk
                        for (byte chunkTileX = 0; chunkTileX < Config.CHUNK_SIZE; chunkTileX++)
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
    }
}


