using System.Collections.Concurrent;
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

    // Accessors
    public static (byte material, TileSurface surface, TileProperties properties) GetTileAt(Position pos)
    {
        var chunkPos = pos.ChunkPosition;
        var (X, Y) = pos.TilePosition;
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

    // Bounds checking
    public static bool IsInChunkBounds(byte X, byte Y) =>
        X >= 0 && X < Config.CHUNK_SIZE && Y >= 0 && Y < Config.CHUNK_SIZE;

    public static bool IsInWorldBounds(int X, int Y) =>
        X >= 0 && X < Config.WORLD_SIZE && Y >= 0 && Y < Config.WORLD_SIZE;






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
            for (byte x = 0; x < Config.WORLD_SIZE; x++)
            {
                for (byte y = 0; y < Config.WORLD_SIZE; y++)
                {
                    var chunk = new Chunk(x, y);
                    GenerateChunk(chunk);
                    ZoneManager.CreateZones(chunk);
                    _chunks[(x, y)] = chunk;
                }
            }
        }

        private static void GenerateChunk(Chunk chunk)
        {
            var (materials, properties) = GenerateChunkTerrain(chunk.X, chunk.Y);

            // Apply the generated base terrain
            for (byte x = 0; x < Config.CHUNK_SIZE; x++)
            {
                for (byte y = 0; y < Config.CHUNK_SIZE; y++)
                {
                    byte index = (byte)(x * Config.CHUNK_SIZE + y);
                    // Surface is initially None - other systems will handle dynamic surfaces
                    chunk.SetTile(x, y, materials[index], TileSurface.None, properties[index]);
                }
            }
        }

        private static (byte[] materials, TileProperties[] properties)
            GenerateChunkTerrain(byte chunkX, byte chunkY)
        {
            var size = Config.CHUNK_SIZE * Config.CHUNK_SIZE;
            var materials = new byte[size];
            var properties = new TileProperties[size];

            float baseScale = 0.05f; // Adjust scale for terrain size

            for (byte localX = 0; localX < Config.CHUNK_SIZE; localX++)
            {
                for (byte localY = 0; localY < Config.CHUNK_SIZE; localY++)
                {
                    float worldX = chunkX * Config.CHUNK_SIZE + localX;
                    float worldY = chunkY * Config.CHUNK_SIZE + localY;

                    // Generate base terrain elevation
                    float elevation = EnhancedPerlinNoise.GenerateTerrain(worldX * baseScale, worldY * baseScale);

                    byte index = (byte)(localX * Config.CHUNK_SIZE + localY);
                    (materials[index], properties[index]) = DetermineTileType(elevation);
                }
            }

            return (materials, properties);
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


