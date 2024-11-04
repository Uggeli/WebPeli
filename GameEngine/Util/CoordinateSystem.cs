namespace WebPeli.GameEngine.Util;

public static class CoordinateSystem
{
    /// <summary>
    /// Explanation of coordinate spaces:
    /// 1. World Space: 
    ///    - Represents the entire game world in terms of individual tiles.
    ///    - Coordinates are always integers.
    ///    - Origin (0,0) is typically at the top-left corner of the world.
    ///    - Example: (150, 75) might represent the 150th tile from the left and 75th from the top.
    /// 
    /// 2. Chunk Space:
    ///    - Represents the grid of chunks that make up the world.
    ///    - Coordinates are always integers.
    ///    - Each unit represents one chunk, not one tile.
    ///    - Example: (3, 2) represents the chunk that is 3 chunks from the left and 2 from the top.
    /// 
    /// 3. Local Space:
    ///    - Represents the position within a single chunk.
    ///    - Coordinates are always integers, typically bytes due to the small size.
    ///    - Origin (0,0) is at the top-left corner of the chunk.
    ///    - Range is limited by chunk size (e.g., 0 to 3 for 4x4 chunks).
    ///    - Example: (1, 3) represents the 2nd tile from the left and 4th from the top within a chunk.
    /// 
    /// 4. Screen Space:
    ///    - Represents pixel coordinates on the player's screen.
    ///    - Coordinates are typically floating-point numbers for smooth rendering and input.
    ///    - Origin (0,0) is at the top-left corner of the screen.
    ///    - The range depends on the screen resolution (e.g., 0 to 1920 for width in 1080p).
    ///    - Example: (960, 540) would be the center of a 1920x1080 screen.
    /// </summary>

    // World and Chunk conversions
    public static (int ChunkX, int ChunkY) WorldToChunk(int worldX, int worldY)
    {
        return (worldX / Config.CHUNK_SIZE, worldY / Config.CHUNK_SIZE);
    }

    public static (int WorldX, int WorldY) ChunkToWorld(int chunkX, int chunkY)
    {
        return (chunkX * Config.CHUNK_SIZE, chunkY * Config.CHUNK_SIZE);
    }

    // World and Local conversions
    public static (byte LocalX, byte LocalY) WorldToLocal(int worldX, int worldY)
    {
        return ((byte)(worldX % Config.CHUNK_SIZE), (byte)(worldY % Config.CHUNK_SIZE));
    }

    public static (int WorldX, int WorldY) ChunkAndLocalToWorld(int chunkX, int chunkY, byte localX, byte localY)
    {
        return (chunkX * Config.CHUNK_SIZE + localX, chunkY * Config.CHUNK_SIZE + localY);
    }

    public static (byte ChunkX, byte ChunkY, byte LocalX, byte LocalY) WorldToChunkAndLocal(int worldX, int worldY)
    {
        byte chunkX = worldX / Config.CHUNK_SIZE;
        byte chunkY = worldY / Config.CHUNK_SIZE;
        byte localX = (byte)(worldX % Config.CHUNK_SIZE);
        byte localY = (byte)(worldY % Config.CHUNK_SIZE);
        return (chunkX, chunkY, localX, localY);
    }

    // Screen and World conversions
    public static (float ScreenX, float ScreenY) WorldToScreen(
        int worldX, int worldY, 
        float screenWidth = Config.SCREENWIDTH, 
        float screenHeight = Config.SCREENHEIGHT,
        float? worldWidth = null, 
        float? worldHeight = null)
    {
        worldWidth ??= Config.WORLD_SIZE * Config.CHUNK_SIZE;
        worldHeight ??= Config.WORLD_SIZE * Config.CHUNK_SIZE;

        float worldLeft = Config.CameraX - worldWidth.Value / 2;
        float worldTop = Config.CameraY + worldHeight.Value / 2;

        float screenX = (worldX - worldLeft) / worldWidth.Value * screenWidth;
        float screenY = (worldTop - worldY) / worldHeight.Value * screenHeight;

        return (screenX, screenY);
    }

    public static (int WorldX, int WorldY) ScreenToWorld(
        float screenX, float screenY, 
        float screenWidth = Config.SCREENWIDTH, 
        float screenHeight = Config.SCREENHEIGHT,
        float? worldWidth = null, 
        float? worldHeight = null)
    {
        worldWidth ??= Config.WORLD_SIZE * Config.CHUNK_SIZE;
        worldHeight ??= Config.WORLD_SIZE * Config.CHUNK_SIZE;

        float worldLeft = Config.CameraX - worldWidth.Value / 2;
        float worldTop = Config.CameraY + worldHeight.Value / 2;

        int worldX = (int)(worldLeft + screenX / screenWidth * worldWidth.Value);
        int worldY = (int)(worldTop - screenY / screenHeight * worldHeight.Value);

        return (worldX, worldY);
    }

    // Screen and Chunk conversions
    public static (float ScreenX, float ScreenY) ChunkToScreen(
        int chunkX, int chunkY, 
        float screenWidth = Config.SCREENWIDTH, 
        float screenHeight = Config.SCREENHEIGHT,
        float? worldWidth = null, 
        float? worldHeight = null)
    {
        var (worldX, worldY) = ChunkToWorld(chunkX, chunkY);
        return WorldToScreen(worldX, worldY, screenWidth, screenHeight, worldWidth, worldHeight);
    }

    public static (int ChunkX, int ChunkY) ScreenToChunk(
        float screenX, float screenY, 
        float screenWidth = Config.SCREENWIDTH, 
        float screenHeight = Config.SCREENHEIGHT,
        float? worldWidth = null, 
        float? worldHeight = null)
    {
        var (worldX, worldY) = ScreenToWorld(screenX, screenY, screenWidth, screenHeight, worldWidth, worldHeight);
        return WorldToChunk(worldX, worldY);
    }

    // Screen and Local conversions
    public static (float ScreenX, float ScreenY) LocalToScreen(
        int chunkX, int chunkY, byte localX, byte localY, 
        float screenWidth = Config.SCREENWIDTH, 
        float screenHeight = Config.SCREENHEIGHT,
        float? worldWidth = null, 
        float? worldHeight = null)
    {
        var (worldX, worldY) = ChunkAndLocalToWorld(chunkX, chunkY, localX, localY);
        return WorldToScreen(worldX, worldY, screenWidth, screenHeight, worldWidth, worldHeight);
    }

    public static (int ChunkX, int ChunkY, byte LocalX, byte LocalY) ScreenToLocal(
        float screenX, float screenY, 
        float screenWidth = Config.SCREENWIDTH, 
        float screenHeight = Config.SCREENHEIGHT,
        float? worldWidth = null, 
        float? worldHeight = null)
    {
        var (worldX, worldY) = ScreenToWorld(screenX, screenY, screenWidth, screenHeight, worldWidth, worldHeight);
        return WorldToChunkAndLocal(worldX, worldY);
    }
}

