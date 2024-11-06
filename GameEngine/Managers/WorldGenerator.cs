using WebPeli.GameEngine.EntitySystem;
using WebPeli.GameEngine.Util;

namespace WebPeli.GameEngine.Managers;

public class WorldGenerator
{
    private const float NOISE_SCALE = 0.3f;  // Adjust this to change terrain feature size
    private const float WALKABLE_THRESHOLD = 0.2f;  // Higher = more walkable areas

    public static void GenerateWorld()
    {
        // Use a random offset for the noise to get different patterns each time
        Random rand = new();
        float offsetX = rand.Next(0, 1000);
        float offsetY = rand.Next(0, 1000);

        // Generate each chunk
        for (byte chunkY = 0; chunkY < Config.WORLD_SIZE; chunkY++)
        {
            for (byte chunkX = 0; chunkX < Config.WORLD_SIZE; chunkX++)
            {
                GenerateChunk(chunkX, chunkY, offsetX, offsetY);
            }
        }

        // Post-process to ensure connectivity
        EnsureWorldConnectivity();
    }

    private static void GenerateChunk(byte chunkX, byte chunkY, float offsetX, float offsetY)
    {
        var chunk = new Chunk();
        for (byte localY = 0; localY < Config.CHUNK_SIZE; localY++)
        {
            for (byte localX = 0; localX < Config.CHUNK_SIZE; localX++)
            {
                // Convert to world coordinates for continuous noise
                float worldX = chunkX * Config.CHUNK_SIZE + localX;
                float worldY = chunkY * Config.CHUNK_SIZE + localY;

                // Generate noise value
                float noiseValue = PerlinNoise.Generate(
                    (worldX + offsetX) * NOISE_SCALE,
                    (worldY + offsetY) * NOISE_SCALE
                );

                // Normalize noise value from [-1, 1] to [0, 1]
                noiseValue = (noiseValue + 1) * 0.5f;

                // Determine tile properties based on noise
                bool isTraversable = noiseValue > WALKABLE_THRESHOLD;
                bool isTransparent = true;  // Most tiles are transparent for now
                byte tileType = DetermineTileType(noiseValue);

                // Set base tile properties
                // byte tile = 0;
                chunk.SetTraversable(localX, localY, isTraversable);
                chunk.SetTransparent(localX, localY, isTransparent);
                chunk.SetTileType(localX, localY, tileType);
            }
        }
        World.SetChunk(chunkX, chunkY, chunk);
    }

    private static byte DetermineTileType(float noiseValue)
    {
        // Convert noise value to tile type (0-3)
        if (noiseValue < 0.3f) return 0;        // Water/Obstacle
        else if (noiseValue < 0.6f) return 1;   // Grass/Path
        else if (noiseValue < 0.8f) return 2;   // Hills/Rough terrain
        else return 3;                          // Mountains/Special
    }

    private static void EnsureWorldConnectivity()
    {
        // Create a minimum spanning path between chunk centers
        for (byte y = 0; y < Config.WORLD_SIZE; y++)
        {
            for (byte x = 0; x < Config.WORLD_SIZE; x++)
            {
                var chunk = World.GetChunk(x, y);
                
                // Create path to right neighbor
                if (x < Config.WORLD_SIZE - 1)
                {
                    CreatePathBetweenChunks(chunk, World.GetChunk(x + 1, y), true);
                }

                // Create path to bottom neighbor
                if (y < Config.WORLD_SIZE - 1)
                {
                    CreatePathBetweenChunks(chunk, World.GetChunk(x, y + 1), false);
                }
            }
        }
    }

    private static void CreatePathBetweenChunks(Chunk? chunk1, Chunk? chunk2, bool horizontal)
    {
        if (chunk1 == null || chunk2 == null) return;
        if (horizontal)
        {
            // Create horizontal connection
            byte y = Config.CHUNK_SIZE / 2;
            // Make the connecting tiles traversable
            chunk1.SetTraversable(Config.CHUNK_SIZE - 1, y, true);
            chunk2.SetTraversable(0, y, true);
            // Set appropriate exits
            chunk1.SetExitEast(Config.CHUNK_SIZE - 1, y, true);
            chunk2.SetExitWest(0, y, true);
        }
        else
        {
            // Create vertical connection
            byte x = Config.CHUNK_SIZE / 2;
            // Make the connecting tiles traversable
            chunk1.SetTraversable(x, Config.CHUNK_SIZE - 1, true);
            chunk2.SetTraversable(x, 0, true);
            // Set appropriate exits
            chunk1.SetExitSouth(x, Config.CHUNK_SIZE - 1, true);
            chunk2.SetExitNorth(x, 0, true);
        }
    }
}