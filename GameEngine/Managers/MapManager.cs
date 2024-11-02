namespace WebPeli.GameEngine.Managers;

public class MapManager : BaseManager
{
    public Chunk[,] Chunks = new Chunk[Config.WORLD_SIZE, Config.WORLD_SIZE];
    public MapManager()
    {
        // EventManager.RegisterListener<ChunkEvent>(this);
        // EventManager.RegisterListener<PathRequest>(this);
        // EventManager.RegisterListener<MoveRequest>(this);
    }

    public override void HandleMessage(IEvent evt)
    {
        // Handle chunk events
        switch (evt)
        {
            // Priority messages
            case TerrainCollisionRequestPriority request:
                bool Result = HandleCollisionCheck(request.X, request.Y);
                EventManager.EmitCallback(request.CallbackId, Result);
                break;
        }
    }

    bool HandleCollisionCheck(int x, int y)
    {
        // X and Y are in world coordinates
        var (chunkX, chunkY, localX, localY) = Util.CoordinateSystem.WorldToChunkAndLocal(x, y);
        var chunk = GetChunk(chunkX, chunkY);
        if (chunk == null)
        {
            return false;
        }
        return chunk.IsTraversable(localX, localY);
    }

    Chunk? GetChunk(int chunkX, int chunkY)
    {
        if (chunkX < 0 || chunkX >= Config.WORLD_SIZE || chunkY < 0 || chunkY >= Config.WORLD_SIZE)
        {
            return null;
        }
        return Chunks[chunkX, chunkY];
    }

    public (byte, byte)[] GetPath(float startScreenX, float startScreenY, float endScreenX, float endScreenY)
    {
        var (startChunkX, startChunkY, startLocalX, startLocalY) = Util.CoordinateSystem.ScreenToLocal(startScreenX, startScreenY);
        var (endChunkX, endChunkY, endLocalX, endLocalY) = Util.CoordinateSystem.ScreenToLocal(endScreenX, endScreenY);

        var startChunk = GetChunk(startChunkX, startChunkY);
        var endChunk = GetChunk(endChunkX, endChunkY);

        if (startChunk == null || endChunk == null)
        {
            return [];
        }
        if (startChunk == endChunk)
        {
            return startChunk.GetPath(startLocalX, startLocalY, endLocalX, endLocalY);
        }
        var interChunkPath = FindInterChunkPath(startChunkX, startChunkY, endChunkX, endChunkY);
        if (interChunkPath.Length > 0)
        {
            List<(byte, byte)> Path = [];
            Chunk? next_chunk = GetChunk(interChunkPath[1].Item1, interChunkPath[1].Item2);
            if (next_chunk == null)
            {
                return [];
            }
            var connectionPoint = GetChunkConnectionPoint(startChunk, next_chunk, (interChunkPath[1].Item1 - startChunkX, interChunkPath[1].Item2 - startChunkY));
            if (connectionPoint == null)
            {
                return [];
            }
            return startChunk.GetPath(startLocalX, startLocalY, connectionPoint.Value.Item1, connectionPoint.Value.Item2);
        }
        return [];
    }

    (int, int)[] FindInterChunkPath(int startChunkX, int startChunkY, int endChunkX, int endChunkY)
    {
        var startChunk = GetChunk(startChunkX, startChunkY);
        var endChunk = GetChunk(endChunkX, endChunkY);
        if (startChunk == null || endChunk == null)
        {
            return [];
        }

        var openSet = new PriorityQueue<(int, int), float>();
        var cameFrom = new Dictionary<(int, int), (int, int)>();

        var gScore = new Dictionary<(int, int), float>();
        var fScore = new Dictionary<(int, int), float>();

        openSet.Enqueue((startChunkX, startChunkY), 0);
        gScore[(startChunkX, startChunkY)] = 0;
        fScore[(startChunkX, startChunkY)] = Heuristic((startChunkX, startChunkY), (endChunkX, endChunkY));

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current == (endChunkX, endChunkY))
            {
                var path = new List<(int, int)>
                {
                    current
                };
                while (cameFrom.ContainsKey(current))
                {
                    current = cameFrom[current];
                    path.Add(current);
                }
                path.Reverse();
                return [.. path];
            }

            foreach (var neighbour in GetNeighboringChunks(current.Item1, current.Item2))
            {
                var tentativeGScore = gScore[current] + 1; // 1 is the distance between two nodes
                if (!gScore.TryGetValue(neighbour, out float value) || tentativeGScore < value)
                {
                    cameFrom[neighbour] = current;
                    value = tentativeGScore;
                    gScore[neighbour] = value;
                    fScore[neighbour] = gScore[neighbour] + Heuristic(neighbour, (endChunkX, endChunkY));
                    openSet.Enqueue(neighbour, fScore[neighbour]);
                }
            }
        }
        return [];
    }

    static float Heuristic((int, int) a, (int, int) b)
    {
        return MathF.Abs(a.Item1 - b.Item1) + MathF.Abs(a.Item2 - b.Item2); // Manhattan distance
    }



    (int, int)[] GetNeighboringChunks(int chunkX, int chunkY)
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

    static (byte, byte)? GetChunkConnectionPoint(Chunk chunk1, Chunk chunk2, (int, int) direction)
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
    public override void Init()
    {
        for (int x = 0; x < Config.WORLD_SIZE; x++)
        {
            for (int y = 0; y < Config.WORLD_SIZE; y++)
            {
                Chunks[x, y] = new Chunk();
            }
        }
        WorldGenerator.GenerateWorld(this);
        System.Console.WriteLine(VisualizeWorld());
    }

    public override void Destroy()
    {

    }

    public string VisualizeChunk(int chunkX, int chunkY)
    {
        var chunk = GetChunk(chunkX, chunkY);
        if (chunk == null) return "Invalid chunk coordinates";

        var visualization = new System.Text.StringBuilder();
        visualization.AppendLine($"Chunk [{chunkX}, {chunkY}]:");

        for (byte y = 0; y < Config.CHUNK_SIZE; y++)
        {
            for (byte x = 0; x < Config.CHUNK_SIZE; x++)
            {
                // Show tile type and traversability
                char symbol = chunk.IsTraversable(x, y) ? '.' : '#';
                byte tileType = chunk.GetTileType(x, y);
                visualization.Append($"{tileType}{symbol} ");
            }
            visualization.AppendLine();
        }

        return visualization.ToString();
    }

    public string VisualizeWorld()
    {
        var visualization = new System.Text.StringBuilder();
        visualization.AppendLine("World Map:");

        for (int y = 0; y < Config.WORLD_SIZE; y++)
        {
            // Print each row of chunks
            for (byte localY = 0; localY < Config.CHUNK_SIZE; localY++)
            {
                for (int x = 0; x < Config.WORLD_SIZE; x++)
                {
                    var chunk = Chunks[x, y];
                    // Print one row of this chunk
                    for (byte localX = 0; localX < Config.CHUNK_SIZE; localX++)
                    {
                        char symbol = chunk.IsTraversable(localX, localY) ? '.' : '#';
                        byte tileType = chunk.GetTileType(localX, localY);
                        visualization.Append($"{tileType}{symbol} ");
                    }
                    visualization.Append("| "); // Chunk boundary
                }
                visualization.AppendLine();
            }
            // Print chunk boundary line
            visualization.AppendLine(new string('-', Config.WORLD_SIZE * (Config.CHUNK_SIZE * 3 + 2)));
        }

        return visualization.ToString();
    }
    private const float MIN_VIEWPORT_SIZE = 100;  // pixels
    private const float MAX_VIEWPORT_SIZE = 2000; // pixels
    public byte[,] GetTilesInArea(
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

                var chunk = GetChunk(chunkX, chunkY);
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
}