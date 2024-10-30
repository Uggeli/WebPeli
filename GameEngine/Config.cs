namespace WebPeli.GameEngine;

static class Config
{
    public const int SCREENWIDTH = 800;
    public const int SCREENHEIGHT = 600;
    public const byte CHUNK_SIZE = 8;
    public const byte WORLD_SIZE = 3;

    // Mutable static fields
    private static float _cameraX;
    private static float _cameraY;

    // Public accessors for camera position
    public static float CameraX => _cameraX;
    public static float CameraY => _cameraY;

    // Event to notify when camera position changes
    public static event Action? OnCameraPositionChanged;

    public static void SetCameraPosition(float x, float y)
    {
        bool changed = x != _cameraX || y != _cameraY;
        _cameraX = x;
        _cameraY = y;
        if (changed)
        {
            OnCameraPositionChanged?.Invoke();
        }
    }
}

