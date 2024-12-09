using System.Collections.Concurrent;
using WebPeli.GameEngine.Util;

namespace WebPeli.GameEngine.World.WorldData;

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
    private readonly TileMaterial[] Material = new TileMaterial[Config.CHUNK_SIZE * Config.CHUNK_SIZE];
    private readonly TileSurface[] Surface = new TileSurface[Config.CHUNK_SIZE * Config.CHUNK_SIZE];
    private static int ConvertTo1D(byte x, byte y) => y * Config.CHUNK_SIZE + x;
    // private static (byte, byte) ConvertTo2D(byte i) => ((byte)(i / Config.CHUNK_SIZE), (byte)(i % Config.CHUNK_SIZE)); 
    public (TileMaterial material, TileSurface surface, TileProperties properties) GetTile(byte x, byte y) => (Material[ConvertTo1D(x, y)], Surface[ConvertTo1D(x, y)], Properties[ConvertTo1D(x, y)]);
    public (TileMaterial material, TileSurface surface, TileProperties properties) GetTile(int x, int y)
    {
        if (!World.IsInChunkBounds(x, y))
        {
            return (0, TileSurface.None, TileProperties.None);
        }
        return (Material[ConvertTo1D((byte)x, (byte)y)], Surface[ConvertTo1D((byte)x, (byte)y)], Properties[ConvertTo1D((byte)x, (byte)y)]);
    }

    public void SetTile(byte x, byte y, TileMaterial material, TileSurface surface, TileProperties properties)
    {
        Material[ConvertTo1D(x, y)] = material;
        Surface[ConvertTo1D(x, y)] = surface;
        Properties[ConvertTo1D(x, y)] = properties;
    }
    public void SetTileBaseMaterial(byte x, byte y, TileMaterial material) => Material[ConvertTo1D(x, y)] = material;
    public void SetTileOverlayMaterial(byte x, byte y, TileSurface material) => Surface[ConvertTo1D(x, y)] = material;
    public void SetTileProperties(byte x, byte y, TileProperties properties) => Properties[ConvertTo1D(x, y)] = properties;

    // Zone data
    private readonly ConcurrentDictionary<int, Zone> _Zones = [];
    public void AddZone(Zone zone) => _Zones[zone.Id] = zone;
    public void SetZones(IEnumerable<Zone> zones)
    {
        _Zones.Clear();
        foreach (var zone in zones)
        {
            _Zones[zone.Id] = zone;
        }
    }
    public void RemoveZone(int id) => _Zones.TryRemove(id, out _);
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
    private readonly ConcurrentDictionary<(byte x, byte y),List<int>> _Entities = [];  // pos within chunk, entity ids
    public IEnumerable<int> GetEntitiesAt(byte x, byte y) => _Entities.TryGetValue((x, y), out List<int>? entities) ? entities : [];
    public bool CanFitEntity(byte x, byte y, byte volume) => TileVolume[ConvertTo1D(x, y)] + volume <= Config.MAX_TILE_VOLUME;
    /// <summary>
    /// Checks if an entity can be added to the chunk.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="volume"></param>
    /// <returns></returns>
    public bool CanAddEntity(IEnumerable<Position> position, byte volume)
    {
        foreach (var pos in position)
        {
            if (pos.ChunkPosition != (X, Y)) continue;
            if (!CanFitEntity(pos.TilePosition.X, pos.TilePosition.Y, volume))
                return false;
        }
        return true;
    }

    public bool CanAddEntity(Position pos, byte volume) => CanFitEntity(pos.TilePosition.X, pos.TilePosition.Y, volume);

    /// <summary>
    /// Adds an entity to the chunk. Does not check if entity can fit.
    /// </summary>
    /// <param name="entityId"></param>
    /// <param name="position"></param>
    /// <param name="volume"></param>
    public void AddEntity(int entityId, IEnumerable<Position> position, byte volume)
    {
        foreach (var pos in position)
        {
            if (pos.ChunkPosition != (X, Y)) continue;
            TileVolume[ConvertTo1D(pos.TilePosition.X, pos.TilePosition.Y)] += volume;
            
            if (!_Entities.ContainsKey((pos.TilePosition.X, pos.TilePosition.Y)))
            {
                _Entities[(pos.TilePosition.X, pos.TilePosition.Y)] = [];
            }
            _Entities[(pos.TilePosition.X, pos.TilePosition.Y)].Add(entityId);
        }
    }

    /// <summary>
    /// Adds an entity to the chunk. Does not check if entity can fit.
    /// </summary>
    /// <param name="entityId"></param>
    /// <param name="position"></param>
    /// <param name="volume"></param>
    public void AddEntity(int entityId, Position position, byte volume)
    {
        if (position.ChunkPosition != (X, Y)) return;

        TileVolume[ConvertTo1D(position.TilePosition.X, position.TilePosition.Y)] += volume;
        
        if (!_Entities.ContainsKey((position.TilePosition.X, position.TilePosition.Y)))
        {
            _Entities[(position.TilePosition.X, position.TilePosition.Y)] = [];
        }
        _Entities[(position.TilePosition.X, position.TilePosition.Y)].Add(entityId);
    }


    /// <summary>
    /// Tries to add an entity to the chunk. Returns true if entity was added successfully, false otherwise.
    /// </summary>
    /// <param name="entityId"></param>
    /// <param name="position"></param>
    /// <param name="volume"></param>
    /// <returns></returns>
    public bool TryToAddEntity(int entityId, IEnumerable<Position> position, byte volume)
    {
        // First check if entity can fit
        if (!CanAddEntity(position, volume))
            return false;
    
        // Add entity to tiles
        foreach (var pos in position)
        {
            if (pos.ChunkPosition != (X, Y)) continue;
            TileVolume[ConvertTo1D(pos.TilePosition.X, pos.TilePosition.Y)] += volume;
            
            if (!_Entities.ContainsKey((pos.TilePosition.X, pos.TilePosition.Y)))
            {
                _Entities[(pos.TilePosition.X, pos.TilePosition.Y)] = [];
            }
            _Entities[(pos.TilePosition.X, pos.TilePosition.Y)].Add(entityId);
        }
        return true;
    }

    /// <summary>
    /// Removes an entity from the chunk.
    /// </summary>
    /// <param name="entityId"></param>
    public void RemoveEntity(int entityId)
    {
        foreach (var entities in _Entities.Values)
        {
            entities.Remove(entityId);
        }
    }
    
    public Position[] GetEntityPositions(int entityId)
    {
        var positions = new List<Position>();
        foreach (var (pos, entities) in _Entities)
        {
            if (entities.Contains(entityId))
            {
                positions.Add(new Position ((X, Y), pos));
            }
        }
        return positions.ToArray();
    }
    
}
