using System.Numerics;
using WebPeli.GameEngine.EntitySystem;
using WebPeli.GameEngine.Managers;

namespace WebPeli.GameEngine;

public static class World
{
    private static Chunk[,] _chunks { get; set; } = new Chunk[Config.WORLD_SIZE, Config.WORLD_SIZE];
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

        return _chunks[chunkX, chunkY].GetEntitiesAt(new EntityPosition(localX, localY));
    }

    public static bool RemoveEntity(Guid entityId)
    {
        foreach (var chunk in _chunks)
        {
            if (chunk.RemoveEntity(entityId)) return true;
        }
        return false;
    }
}