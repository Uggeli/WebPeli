using WebPeli.GameEngine.World.WorldData;

namespace WebPeli.GameEngine;

static class Config
{
    // Game configuration
    public const int TILE_SIZE = 32; // pixels
    public const byte MAX_TILE_VOLUME = 255;
    public const byte CHUNK_SIZE = 128;
    public const byte CHUNK_SIZE_BYTE = CHUNK_SIZE - 1;  // This was terrible idea
    public const byte WORLD_SIZE = 8; // world size = 2^WORLD_SIZE chunks
    public const int WORLD_TILES = WORLD_SIZE * CHUNK_SIZE;

    // Entity configuration
    public const byte MAX_ENTITIES_PER_TILE = 16;  // Or whatever makes sense
    public const byte MAX_ENTITY_SIZE = 4;  // 4x4 tiles max
    public const int MAX_ENTITIES = 1_000_000;  // We can easily change this
    public const int MAX_TREES = 300_000;  // We can easily change this


    // Tile configuration
    public const byte MOISTURE_MIN_DIFFERENCE = 5;
    public const byte WATER_TILE_MOISTURE = 255;  // max moisture
    public const byte MOISTURE_CHECK_INTERVAL = 10;  // ticks

    // World generation
    public static bool GenerateFlatWorld = false;
    public const float WATER_THRESHOLD = -1.5f;
    public const float BEACH_THRESHOLD = -0.8f;
    public const float HILL_THRESHOLD = 2.0f;
    public const float MOUNTAIN_THRESHOLD = 2.5f;
    public const float SNOW_THRESHOLD = 2.6f; // mountain tops


    // Systems configuration
    public const byte HUNGER_RATE = 2;
    public const byte THIRST_RATE = 2;
    public const byte FATIGUE_RATE = 2;
    public const int UpdateLoop = 16; // ms

    // Time configuration
    public const int TicksToUpdateTimeOfDay = 10;  // 1 second
    public const int SpringLength = 5;
    public const int SummerLength = 5;
    public const int AutumnLength = 5;
    public const int WinterLength = 5;

    // Debugging
    public static int LOG_MAX_MESSAGES = 1000;
    public static bool DebugMode = false;
    public static bool DebugPathfinding = false;
}

