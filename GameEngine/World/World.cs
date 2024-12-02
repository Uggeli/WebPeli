using System.Collections.Concurrent;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World.WorldData;

namespace WebPeli.GameEngine.World;


internal static partial class World
{

    // private static readonly int _worldGridSize = Config.WORLD_SIZE * Config.WORLD_SIZE;
    // private static readonly int _chunkSize = Config.CHUNK_SIZE_BYTE * Config.CHUNK_SIZE_BYTE;
    private static ConcurrentDictionary<(int X, int Y), Chunk> _chunks = [];
    private static Dictionary<(int x, int y), ChunkConnection> _chunkGraph = [];


    // Accessors, Map data
    public static (byte material, TileSurface surface, TileProperties properties) GetTileAt(Position pos)
    {
        (byte X, byte Y) chunkPos = pos.ChunkPosition;
        if (!IsInWorldBounds(pos)) return (0, TileSurface.None, TileProperties.None);
        (byte X, byte Y) = pos.TilePosition;
        if (!IsInChunkBounds(X, Y)) return (0, TileSurface.None, TileProperties.None);
        return _chunks[chunkPos].GetTile(X, Y);
    }

    public static void SetTileAt(Position pos, byte material, TileSurface surface, TileProperties properties)
    {
        (byte X, byte Y) chunkPos = pos.ChunkPosition;
        (byte X, byte Y) = pos.TilePosition;
        _chunks[chunkPos].SetTile(X, Y, material, surface, properties);
    }

    public static (byte material, TileSurface surface, TileProperties props)[] GetTilesInArea(Position topLeft, int width, int height)
    {
        var tiles = new (byte material, TileSurface surface, TileProperties props)[width * height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Position pos = topLeft + (x, y);
                tiles[y * width + x] = GetTileAt(pos);
            }
        }
        return tiles;
    }


    public static Chunk? GetChunk((int X, int Y) pos)
    {
        return _chunks.TryGetValue(pos, out Chunk? c) ? c : null;
    }

    public static Chunk? GetChunk(Position pos)
    {
        return GetChunk(pos.ChunkPosition);
    }


    public static void SetChunk((byte X, byte Y) pos, Chunk chunk)
    {
        _chunks[pos] = chunk;
    }

    // Bounds checking
    public static bool IsInChunkBounds(byte x, byte y) =>
        x < Config.CHUNK_SIZE && y < Config.CHUNK_SIZE;

    public static bool IsInChunkBounds(int x, int y) =>
        x >= 0 && x < Config.CHUNK_SIZE &&
        y >= 0 && y < Config.CHUNK_SIZE;

    public static bool IsInChunkBounds(Position pos) =>
        IsInChunkBounds(pos.TilePosition.X, pos.TilePosition.Y);

    public static bool IsInWorldBounds(int x, int y) =>
        x >= 0 && x < Config.WORLD_SIZE * Config.CHUNK_SIZE &&
        y >= 0 && y < Config.WORLD_SIZE * Config.CHUNK_SIZE;

    public static bool IsInWorldBounds(Position pos) => IsInWorldBounds(pos.ChunkPosition.X, pos.ChunkPosition.Y);

    // World generation
    public static void GenerateWorld()
    {
        WorldGenerator.GenerateWorld();
    }
}


