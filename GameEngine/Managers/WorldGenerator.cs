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

        // Generate each chunk and immediately build its zones
        for (byte chunkY = 0; chunkY < Config.WORLD_SIZE; chunkY++)
        {
            for (byte chunkX = 0; chunkX < Config.WORLD_SIZE; chunkX++)
            {
                // Generate basic chunk
                GenerateChunk(chunkX, chunkY, offsetX, offsetY);

                // Build zones for this chunk right away
                var chunk = World.GetChunk(chunkX, chunkY);
                if (chunk != null)
                {
                    var zones = World.FindChunkZones(chunk);
                    World.BuildChunkExits(chunk);
                    World._chunkZones[(chunkX, chunkY)] = zones;
                }
            }
        }

        // Connect all zones between chunks
        World.ConnectZones();

    }

    private static void GenerateChunk(byte chunkX, byte chunkY, float offsetX, float offsetY)
    {
        var chunk = new Chunk(chunkX, chunkY);
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
}