using WebPeli.GameEngine.World.WorldData;

namespace WebPeli.GameEngine;

static class Config
{
    public const int SCREENWIDTH = 800;
    public const int SCREENHEIGHT = 600;
    public const int TILE_SIZE = 32; // pixels
    public const byte MAX_TILE_VOLUME = 255;
    public const byte CHUNK_SIZE = 16;
    public const byte CHUNK_SIZE_BYTE = CHUNK_SIZE - 1;
    public const byte WORLD_SIZE = 8; // world size = 2^WORLD_SIZE chunks
    public const int WORLD_TILES = WORLD_SIZE * CHUNK_SIZE;

    // Entity configuration
    public const byte MAX_ENTITIES_PER_TILE = 16;  // Or whatever makes sense
    public const byte MAX_ENTITY_SIZE = 4;  // 4x4 tiles max
    public const int MAX_ENTITIES = 1_000_000;  // We can easily change this


    // Tile configuration
    public const byte MOISTURE_MIN_DIFFERENCE = 5;
    public const byte WATER_TILE_MOISTURE = 255;  // max moisture
    public const byte MOISTURE_CHECK_INTERVAL = 10;  // ticks

    // World generation
    public static bool GenerateFlatWorld = false;
    public const float WATER_THRESHOLD = -0.4f;
    public const float BEACH_THRESHOLD = 0.2f;
    public const float HILL_THRESHOLD = 1.5f;
    public const float MOUNTAIN_THRESHOLD = 2.0f;
    public const float SNOW_THRESHOLD = 2.1f; // mountain tops


    // Systems configuration
    public const byte HUNGER_RATE = 2;
    public const byte THIRST_RATE = 2;
    public const byte FATIGUE_RATE = 2;
    public const int UpdateLoop = 16; // ms

    // Time configuration
    public const int TicksToUpdateTimeOfDay = 1000;  // 1 second

    // Debugging
    public static bool DebugMode = false;
    public static bool DebugPathfinding = false;
}

