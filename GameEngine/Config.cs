namespace WebPeli.GameEngine;

static class Config
{
    public const int SCREENWIDTH = 800;
    public const int SCREENHEIGHT = 600;
    public const int TILE_SIZE = 32; // pixels
    public const byte MAX_TILE_VOLUME = 255;
    public const byte CHUNK_SIZE = 128;
    public const byte CHUNK_SIZE_BYTE = CHUNK_SIZE - 1;
    public const byte WORLD_SIZE = 8; // world size = 2^WORLD_SIZE chunks
    public const int WORLD_TILES = WORLD_SIZE * CHUNK_SIZE;

    // Entity configuration
    public const byte MAX_ENTITIES_PER_TILE = 16;  // Or whatever makes sense
    public const byte MAX_ENTITY_SIZE = 4;  // 4x4 tiles max
    public const int MAX_ENTITIES = 1_000_000;  // We can easily change this


    // Systems configuration
    public const byte HUNGER_RATE = 2;
    public const byte THIRST_RATE = 2;
    public const byte FATIGUE_RATE = 2;

    public const int UpdateLoop = 16; // ms

    // Debugging
    public static bool DebugMode = true;
    public static bool DebugPathfinding = false;
    public static bool GenerateFlatWorld = true;
}

