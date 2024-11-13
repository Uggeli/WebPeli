using System.Collections.Concurrent;
namespace WebPeli.GameEngine.Managers;

public class Chunk(byte x, byte y)
{
    public byte X { get; } = x;
    public byte Y { get; } = y;

    private readonly byte[,] tiles = new byte[Config.CHUNK_SIZE, Config.CHUNK_SIZE];
    private readonly byte[,] tileTextures = new byte[Config.CHUNK_SIZE, Config.CHUNK_SIZE];  // Later: used for rendering
    private readonly Dictionary<(byte X, byte Y), HashSet<Guid>> _positionMap = [];
    private readonly Dictionary<Guid, HashSet<(byte X, byte Y)>> _entityPositions = [];

    public bool AddEntity(Guid entityId, IEnumerable<(byte X, byte Y)> positions)
    {
        if (HasCollision(positions)) return false;

        var positionsSet = new HashSet<(byte X, byte Y)>(positions);
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

    public IEnumerable<Guid> GetEntitiesAt((byte X, byte Y) position) =>
        _positionMap.TryGetValue(position, out var entities) ? entities : [];

    public bool UpdateEntityPosition(Guid entityId, (byte X, byte Y) newPos)
    {
        if (!_entityPositions.ContainsKey(entityId)) return false;
        return UpdateEntityPositions(entityId, [newPos]);
    }

    public bool UpdateEntityPositions(Guid entityId, IEnumerable<(byte X, byte Y)> newPositions)
    {
        if (!ValidatePositions(newPositions)) return false;
        if (HasCollision(newPositions)) return false;

        RemoveEntity(entityId);
        return AddEntity(entityId, newPositions);
    }

    private static bool ValidatePositions(IEnumerable<(byte X, byte Y)> positions)
    {
        foreach (var pos in positions)
        {
            if (!World.IsInChunkBounds(pos.X, pos.Y)) return false;
        }
        return true;
    }

    private bool HasCollision(IEnumerable<(byte X, byte Y)> positions)
    {
        return positions.Any(pos => 
            !IsTraversable(pos.X, pos.Y) || 
            (_positionMap.ContainsKey(pos) && _positionMap[pos].Count > 0));
    }

    public void SetTile(byte x, byte y, byte tileData)
    {
        if (!World.IsInChunkBounds(x, y)) return;
        tiles[x, y] = tileData;
    }

    public byte GetTile(byte x, byte y)
    {
        if (!World.IsInChunkBounds(x, y)) return 0;
        return tiles[x, y];
    }

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
}