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

public static class World
{

    private static Chunk[,] _chunks { get; set; } = new Chunk[Config.WORLD_SIZE, Config.WORLD_SIZE];
    private static Dictionary<Guid, EntityState> _entityStates = [];

    public static void SetEntityState(Guid entityId, EntityState state)
    {
        _entityStates[entityId] = state;
    }

    public static EntityState? GetEntityState(Guid entityId)
    {
        if (_entityStates.TryGetValue(entityId, out EntityState state))
        {
            return state;
        }
        return null;
    }

    public static Chunk? GetChunk(byte x, byte y)
    {
        if (!IsInWorldBounds(x, y))
            return null;
        return _chunks[x, y];
    }

    public static Chunk? GetChunk(int x, int y)
    {
        if (!IsInWorldBounds((byte)x, (byte)y))
            return null;
        return _chunks[x, y];
    }

    public static void SetChunk(byte x, byte y, Chunk chunk)
    {
        if (!IsInWorldBounds(x, y))
            return;
        _chunks[x, y] = chunk;
    }

    private static bool IsInWorldBounds(byte x, byte y)
    {
        return x >= 0 && x < Config.WORLD_SIZE && y >= 0 && y < Config.WORLD_SIZE;
    }

    public static (int, int)[] GetNeighboringChunks(int chunkX, int chunkY)
    {
        Chunk? chunk = GetChunk(chunkX, chunkY);
        if (chunk == null)
        {
            return [];
        }

        List<(int, int)> neighbourSpots = [
            (0,-1),
            (1, 0),
            (0, 1),
            (-1, 0)
        ];
        List<(int, int)> connectedChunks = [];

        foreach (var (x, y) in neighbourSpots)
        {
            var neighbour = GetChunk(x + chunkX, y + chunkY);
            if (neighbour == null) continue;
            if (GetChunkConnectionPoint(chunk, neighbour, (x, y)) != null)
            {
                connectedChunks.Add((x + chunkX, y + chunkY));
            }
        }
        return [.. connectedChunks];
    }

    public static (byte, byte)? GetChunkConnectionPoint(Chunk chunk1, Chunk chunk2, (int, int) direction)
    {
        switch (direction)
        {
            case (0, -1): // North
                for (byte x = 0; x < Config.CHUNK_SIZE; x++)
                {
                    if (chunk1.GetExitNorth(x, 0) && chunk2.GetExitSouth(x, 0))
                    {
                        return (x, 0);
                    }
                }
                break;
            case (1, 0): // East
                for (byte y = 0; y < Config.CHUNK_SIZE; y++)
                {
                    if (chunk1.GetExitEast(Config.CHUNK_SIZE - 1, y) && chunk2.GetExitWest(0, y))
                    {
                        return (Config.CHUNK_SIZE - 1, y);
                    }
                }
                break;
            case (0, 1): // South
                for (byte x = 0; x < Config.CHUNK_SIZE; x++)
                {
                    if (chunk1.GetExitSouth(x, Config.CHUNK_SIZE - 1) && chunk2.GetExitNorth(x, 0))
                    {
                        return (x, Config.CHUNK_SIZE - 1);
                    }
                }
                break;
            case (-1, 0): // West
                for (byte y = 0; y < Config.CHUNK_SIZE; y++)
                {
                    if (chunk1.GetExitWest(0, y) && chunk2.GetExitEast(Config.CHUNK_SIZE - 1, y))
                    {
                        return (0, y);
                    }
                }
                break;
        }
        return null;
    }

    public static IEnumerable<Guid> GetEntitiesAt(Vector2 position)
    {
        var (chunkX, chunkY, localX, localY) =
            Util.CoordinateSystem.WorldToChunkAndLocal(position.X, position.Y);
        if (!IsInWorldBounds(chunkX, chunkY))
            return [];
        return _chunks[chunkX, chunkY].GetEntitiesAt(new EntityPosition(localX, localY));
    }

    public static void UpdateEntityPosition(Guid entityId, EntityPosition position)
    {
        var (chunkX, chunkY, localX, localY) =
            Util.CoordinateSystem.WorldToChunkAndLocal(position.X, position.Y);
        if (!IsInWorldBounds(chunkX, chunkY))
            return;
        SetEntityState(entityId, new EntityState
        {
            Position = position,
            Rotation = GetEntityState(entityId)?.Rotation ?? 0f,
            Current = GetEntityState(entityId)?.Current ?? CurrentAction.Idle
        });
        _chunks[chunkX, chunkY].UpdateEntityPosition(entityId, new EntityPosition(localX, localY));
    }

    public static bool RemoveEntity(Guid entityId)
    {
        _entityStates.Remove(entityId);
        foreach (var chunk in _chunks)
        {
            _entityStates.Remove(entityId);
            if (chunk.RemoveEntity(entityId)) return true;
        }
        return false;
    }

    public static void AddEntity(Guid entityId, EntityPosition position)
    {
        // TODO: add multiposition support
        var (chunkX, chunkY, localX, localY) =
            Util.CoordinateSystem.WorldToChunkAndLocal(position.X, position.Y);
        if (!IsInWorldBounds(chunkX, chunkY))
            return;
        _chunks[chunkX, chunkY].AddEntity(entityId, [new EntityPosition(localX, localY)]);
    }

    public static void AddEntity(Guid entityId)
    {
        var random = new Random();
        // var randomChunk = new Vector2(random.Next(0, Config.WORLD_SIZE), random.Next(0, Config.WORLD_SIZE));
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

    private const float MIN_VIEWPORT_SIZE = 100;  // pixels
    private const float MAX_VIEWPORT_SIZE = 2000; // pixels
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
}