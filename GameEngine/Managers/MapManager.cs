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
}