using System.Collections;
using System.IO.Compression;
using WebPeli.GameEngine.Util;
namespace WebPeli.GameEngine.WorldData;

public class Chunk(byte x, byte y)
{
    // Chunk data
    public byte X { get; } = x;
    public byte Y { get; } = y;
    public ChunkConnection Connections { get; set; } = ChunkConnection.None;
    public bool IsConnected(ChunkConnection connection) => (Connections & connection) == connection;
    public void Connect(ChunkConnection connection) => Connections |= connection;
    public void Disconnect(ChunkConnection connection) => Connections &= ~connection;
    
    // Tile data
    private readonly TileProperties[] Properties = new TileProperties[Config.CHUNK_SIZE * Config.CHUNK_SIZE];
    private readonly byte[] Material = new byte[Config.CHUNK_SIZE * Config.CHUNK_SIZE];
    private readonly TileSurface[] Surface = new TileSurface[Config.CHUNK_SIZE * Config.CHUNK_SIZE];
    private static int ConvertTo1D(byte x, byte y) => y * Config.CHUNK_SIZE + x;
    private static (byte, byte) ConvertTo2D(byte i) => ((byte)(i / Config.CHUNK_SIZE), (byte)(i % Config.CHUNK_SIZE));
    public (byte material, TileSurface surface, TileProperties properties) GetTile(byte x, byte y) => (Material[ConvertTo1D(x, y)], Surface[ConvertTo1D(x, y)], Properties[ConvertTo1D(x, y)]);
    public (byte material, TileSurface surface, TileProperties properties) GetTile(int x, int y)
    {
        if (x < 0 || x >= Config.CHUNK_SIZE || y < 0 || y >= Config.CHUNK_SIZE)
        {
            return (0, TileSurface.None, TileProperties.None);
        }
        return (Material[ConvertTo1D((byte)x, (byte)y)], Surface[ConvertTo1D((byte)x, (byte)y)], Properties[ConvertTo1D((byte)x, (byte)y)]);
    }

    public void SetTile(byte x, byte y, byte material, TileSurface surface, TileProperties properties)
    {
        Material[ConvertTo1D(x, y)] = material;
        Surface[ConvertTo1D(x, y)] = surface;
        Properties[ConvertTo1D(x, y)] = properties;
    }
    public void SetTileBaseMaterial(byte x, byte y, byte material) => Material[ConvertTo1D(x, y)] = material;
    public void SetTileOverlayMaterial(byte x, byte y, TileSurface material) => Surface[ConvertTo1D(x, y)] = material;
    public void SetTileProperties(byte x, byte y, TileProperties properties) => Properties[ConvertTo1D(x, y)] = properties;

    // Zone data
    private readonly Dictionary<int, Zone> _Zones = [];
    public void AddZone(Zone zone) => _Zones[zone.Id] = zone;
    public void RemoveZone(int id) => _Zones.Remove(id);
    public Zone GetZone(int id) => _Zones[id];
    public IEnumerable<Zone> GetZones() => _Zones.Values;
    public Zone? GetZoneAt(byte x, byte y)
    {
        foreach (var zone in _Zones.Values)
        {
            if (zone.TilePositions.Contains((x, y)))
            {
                return zone;
            }
        }
        return null;
    }

    // Entity data
    private readonly byte[] TileVolume = new byte[Config.CHUNK_SIZE * Config.CHUNK_SIZE];
    public bool CanFitEntity(byte x, byte y, byte volume) => TileVolume[ConvertTo1D(x, y)] + volume <= Config.MAX_TILE_VOLUME;
    

}

[Flags]
public enum ChunkConnection : byte
{
    None = 0,
    NorthSouth = 1 << 0, // 1
    NorthEast = 1 << 1,  // 2
    NorthWest = 1 << 2,  // 4
    SouthEast = 1 << 3,  // 8
    SouthWest = 1 << 4,  // 16
    EastWest = 1 << 5    // 32
}

// N<->S (1 bit)
// N<->E (1 bit)
// N<->W (1 bit)
// S<->E (1 bit)
// S<->W (1 bit)
// E<->W (1 bit)





// Tile Consists of 3 bytes
// 1. Material
// 2. Surface <- whatever is on top of the material, rain, snow, blood, etc.
// 3. Properties <- can we walk on it, is it solid, does it block light, etc.


[Flags]
public enum TileProperties : byte
{
    None = 0,
    Walkable = 1 << 0,        // 1
    BlocksLight = 1 << 1,     // 2
    Transparent = 1 << 2,     // 4
    BlocksProjectiles = 1 << 3,// 8
    Solid = 1 << 4,           // 16
    Interactive = 1 << 5,     // 32
    Breakable = 1 << 6,       // 64
    Reserved = 1 << 7         // 128
}

public enum TileMaterial : byte
{
    None = 0,
    Dirt = 1,
    Stone = 2,
    Wood = 3,
    Metal = 4,
    Ice = 5,
    Sand = 6,
    Water = 8,
    Lava = 9,
    Snow = 10,
    Blood = 12,  // Tile made of blood, Fucking metal ,\m/
    Mud = 13,
}

[Flags]
public enum TileSurface : byte
{
    None = 0,
    Grass = 1 << 0,      // Can have grass with snow on it
    Snow = 1 << 1,       // Snow covering grass
    Moss = 1 << 2,       // Moss growing alongside grass
    Water = 1 << 3,      // Puddle on grass
    Blood = 1 << 4,      // Blood stains on snow
    Mud = 1 << 5,        // Mud mixed with grass
    Reserved1 = 1 << 6,
    Reserved2 = 1 << 7
}

public static class TileManager
{
    /// <summary>
    /// Combines multiple TileProperties into a single byte representation.
    /// </summary>
    /// <param name="properties">The collection of TileProperties to combine.</param>
    /// <returns>A byte representing the combined TileProperties.</returns>
    public static byte CreateTileProperties(IEnumerable<TileProperties> properties)
    {
        byte result = 0;
        foreach (var property in properties)
        {
            result |= (byte)property;
        }
        return result;
    }

    public static IEnumerable<TileProperties> GetTileProperties(byte properties)
    {
        foreach (TileProperties property in Enum.GetValues<TileProperties>())
        {
            if ((properties & (byte)property) == (byte)property)
            {
                yield return property;
            }
        }
    }    

    /// <summary>
    /// Combines multiple TileSurface values into a single byte representation.
    /// </summary>
    /// <param name="surfaces">The collection of TileSurface values to combine.</param>
    /// <returns>A byte representing the combined TileSurface values.</returns>
    public static byte CreateTileSurface(IEnumerable<TileSurface> surfaces)
    {
        byte result = 0;
        foreach (var surface in surfaces)
        {
            result |= (byte)surface;
        }
        return result;
    }

    public static IEnumerable<TileSurface> GetTileSurfaces(byte surfaces)
    {
        foreach (TileSurface surface in Enum.GetValues<TileSurface>())
        {
            if ((surfaces & (byte)surface) == (byte)surface)
            {
                yield return surface;
            }
        }
    }

    public static (byte material, byte surface, byte properties) CreateTile(TileMaterial material, IEnumerable<TileSurface> surfaces, IEnumerable<TileProperties> properties)
    {
        return ((byte)material, CreateTileSurface(surfaces), CreateTileProperties(properties));
    }

    // Properties
    public static bool HasProperty(TileProperties properties, TileProperties property) => (properties & property) == property;
    public static void SetProperty(ref TileProperties properties, TileProperties property) => properties |= property;
    public static void RemoveProperty(ref TileProperties properties, TileProperties property) => properties &= ~property;
    public static bool IsWalkable(TileProperties properties) => (properties & TileProperties.Walkable) == TileProperties.Walkable;
    public static bool BlocksLight(TileProperties properties) => (properties & TileProperties.BlocksLight) == TileProperties.BlocksLight;
    public static bool IsTransparent(TileProperties properties) => (properties & TileProperties.Transparent) == TileProperties.Transparent;
    public static bool BlocksProjectiles(TileProperties properties) => (properties & TileProperties.BlocksProjectiles) == TileProperties.BlocksProjectiles;
    public static bool IsSolid(TileProperties properties) => (properties & TileProperties.Solid) == TileProperties.Solid;
    public static bool IsInteractive(TileProperties properties) => (properties & TileProperties.Interactive) == TileProperties.Interactive;
    public static bool IsBreakable(TileProperties properties) => (properties & TileProperties.Breakable) == TileProperties.Breakable;
    // Surface and Material stuff
    public static void SetMaterial(ref byte material, TileMaterial materialType) => material = (byte)materialType;
    public static bool IsMaterial(byte material, TileMaterial materialType) => material == (byte)materialType;
    public static bool HasSurface(TileSurface surface, TileSurface surfaceType) => (surface & surfaceType) == surfaceType;
    public static void AddSurface(ref TileSurface surface, TileSurface surfaceType) => surface |= surfaceType;
    public static void RemoveSurface(ref TileSurface surface, TileSurface surfaceType) => surface &= ~surfaceType;
    public static void ClearSurface(ref TileSurface surface) => surface = 0;
}


// Zone is a collection of tiles that are connected to each other and have some kind of edge
// Zone can be used to determine if a tile is part of a room, a cave, a building, etc.
public struct Zone(int id, byte chunkX, byte chunkY, IEnumerable<(byte X, byte Y)> positions, Dictionary<(byte X, byte Y), ZoneEdge> edges)
{
    public readonly int Id { get; init; } = id;
    public readonly Dictionary<(byte X, byte Y), ZoneEdge> Edges { get; init; } = edges;
    public readonly (byte X, byte Y) ChunkPosition { get; init; } = (chunkX, chunkY);
    public HashSet<(byte X, byte Y)> TilePositions { get; set; } = positions.ToHashSet();
}

[Flags]
public enum ZoneEdge : byte
{
    None = 0,
    // Edges within chunk
    North = 1 << 0,     // 1
    South = 1 << 1,     // 2
    East = 1 << 2,      // 4
    West = 1 << 3,      // 8
    // Chunk boundary edges
    ChunkNorth = 1 << 4, // 16
    ChunkSouth = 1 << 5, // 32
    ChunkEast = 1 << 6,  // 64
    ChunkWest = 1 << 7   // 128
}

public static class ZoneManager
{
    public static void CreateZones(Chunk chunk)
    {
        bool[,] visited = new bool[Config.CHUNK_SIZE, Config.CHUNK_SIZE];
        for (byte y = 0; y < Config.CHUNK_SIZE; y++)
        {
            for (byte x = 0; x < Config.CHUNK_SIZE; x++)
            {
                if (visited[x, y]) continue;
                Zone? newZone = DiscoverZone(chunk, (x, y), ref visited);
                if (newZone != null && newZone is Zone zone)
                {
                    chunk.AddZone(zone);
                }
            }
        }
    }

    public static Zone? DiscoverZone(Chunk chunk, (byte x, byte y) startPos, ref bool[,] visited)
    {
        // Find tiles for zone
        List<(byte, byte)> zoneTiles = [];
        Queue<(byte, byte)> openTiles = new();
        openTiles.Enqueue(startPos);
        Dictionary<(byte, byte), ZoneEdge> edges = [];

        while (openTiles.Count > 0)
        {
            (byte x, byte y) = openTiles.Dequeue();
            if (visited[x, y]) continue;
            visited[x, y] = true;    

            if (TileManager.IsWalkable(chunk.GetTile(x, y).properties))
            {
                zoneTiles.Add((x, y));
            }

            var neighbors = new (byte, byte)[]
            {
                (x, (byte)(y - 1)),
                (x, (byte)(y + 1)),
                ((byte)(x - 1), y),
                ((byte)(x + 1), y)
            };

            foreach (var (nx, ny) in neighbors)
            {
                if (!World.IsInWorldBounds(chunk.X * Config.CHUNK_SIZE + x, chunk.Y * Config.CHUNK_SIZE + y)) continue;
                if (!World.IsInChunkBounds(nx, ny)) continue;
                if (visited[nx, ny]) continue;
                openTiles.Enqueue((nx, ny));
            }
        }
        // Found nothing, eatshit
        if (zoneTiles.Count == 0) return null;

        // Edgedetection
        foreach (var (x, y) in zoneTiles)
        {
            
            var neighbors = new (byte, byte)[]
            {
                (x, (byte)(y - 1)),
                (x, (byte)(y + 1)),
                ((byte)(x - 1), y),
                ((byte)(x + 1), y)
            };
            ZoneEdge edge = ZoneEdge.None;
            foreach (var (nx, ny) in neighbors)
            {
                if (!World.IsInWorldBounds(chunk.X * Config.CHUNK_SIZE + x, chunk.Y * Config.CHUNK_SIZE + y)) continue;
                if (!World.IsInChunkBounds(nx, ny))
                {
                    // this neighbour is outside of chunk
                    if (nx < 0)
                    {
                        edge |= ZoneEdge.ChunkWest;
                    }
                    else if (nx >= Config.CHUNK_SIZE)
                    {
                        edge |= ZoneEdge.ChunkEast;
                    }
                    else if (ny < 0)
                    {
                        edge |= ZoneEdge.ChunkNorth;
                    }
                    else if (ny >= Config.CHUNK_SIZE)
                    {
                        edge |= ZoneEdge.ChunkSouth;
                    }
                }
                else if (!zoneTiles.Contains((nx, ny)))
                {
                    // this neighbour is not part of the zone
                    if (nx < 0)
                    {
                        edge |= ZoneEdge.West;
                    }
                    else if (nx >= Config.CHUNK_SIZE)
                    {
                        edge |= ZoneEdge.East;
                    }
                    else if (ny < 0)
                    {
                        edge |= ZoneEdge.North;
                    }
                    else if (ny >= Config.CHUNK_SIZE)
                    {
                        edge |= ZoneEdge.South;
                    }
                }
                if (edge != ZoneEdge.None)
                {
                    edges[(x, y)] = edge;
                }
            }
        }
        var zone = new Zone(IDManager.GetZoneId(), chunk.X, chunk.Y, zoneTiles, edges);
        return zone;
    }


    public static void UpdateZone(Chunk chunk, Zone zone)
    {
        // Remove zone from chunk
        chunk.RemoveZone(zone.Id);
        // Re-create zone
        var visited = new bool[Config.CHUNK_SIZE, Config.CHUNK_SIZE];
        // populate visited with zone tiles
        for (byte y = 0; y < Config.CHUNK_SIZE; y++)
        {
            for (byte x = 0; x < Config.CHUNK_SIZE; x++)
            {
                if (zone.TilePositions.Contains((x, y))) continue;
                visited[x, y] = true;
            }
        }
        DiscoverZone(chunk, zone.TilePositions.First(), ref visited);
    }

    public static List<Zone> GetZones(Chunk chunk)
    {
        return chunk.GetZones().ToList();
    }
}




// long paths tend to be incorrect in long run and long paths take long to run 