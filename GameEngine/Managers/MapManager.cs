using WebPeli.GameEngine.EntitySystem;

namespace WebPeli.GameEngine.Managers;

public class MapManager : BaseManager
{
    public override void HandleMessage(IEvent evt)
    {
        // Handle chunk events
        switch (evt)
        {
            default:
                break;
        }
    }
    public override void Init()
    {
        WorldGenerator.GenerateWorld();
        System.Console.WriteLine(VisualizeWorld());
    }

    public override void Destroy()
    {

    }

    public string VisualizeChunk(int chunkX, int chunkY)
    {
        var chunk = World.GetChunk(chunkX, chunkY);
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
                    var chunk = World.GetChunk(x, y);
                    if (chunk == null) continue;
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
}