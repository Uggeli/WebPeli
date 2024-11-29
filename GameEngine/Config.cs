namespace WebPeli.GameEngine;

static class Config
{
    public const int SCREENWIDTH = 800;
    public const int SCREENHEIGHT = 600;
    public const int TILE_SIZE = 32; // pixels
    public const byte MAX_TILE_VOLUME = 255;
    public const byte CHUNK_SIZE = 8;
    public const byte CHUNK_SIZE_BYTE = CHUNK_SIZE - 1;

    public const byte WORLD_SIZE = 8; // world size = 2^WORLD_SIZE chunks


    // Systems configuration
    public const byte HUNGER_RATE = 2;
    public const byte THIRST_RATE = 2;
    public const byte FATIGUE_RATE = 2;

    public const int UpdateLoop = 16; // ms

    // Debugging
    public static bool DebugMode = true;
    public static bool DebugPathfinding = true;
    public static bool GenerateFlatWorld = true;
}

