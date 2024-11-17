using System.Buffers;
using System.Buffers.Binary;

namespace WebPeli.GameEngine.Managers;

// Network-ready version of viewport data
public readonly record struct ViewportDataBinary
{
    public required Memory<byte> EncodedData { get; init; }

    // Helper to get dimensions from encoded data
    public (ushort Width, ushort Height) GetDimensions()
    {
        var width = BinaryPrimitives.ReadUInt16LittleEndian(EncodedData.Span[0..2]);
        var height = BinaryPrimitives.ReadUInt16LittleEndian(EncodedData.Span[2..4]);
        return (width, height);
    }
}

// Specialized manager for handling viewport requests
public class ViewportManager : BaseManager
{
    private readonly ArrayPool<byte> _arrayPool;
    private readonly ILogger<ViewportManager> _logger;

    public ViewportManager(ILogger<ViewportManager> logger)
    {
        _logger = logger;
        _arrayPool = ArrayPool<byte>.Shared;
        EventManager.RegisterListener<ViewportRequest>(this);
    }

    public override void HandleMessage(IEvent evt)
    {
        if (evt is ViewportRequest req)
        {
            var viewportData = GetViewportDataBinary(
                req.CameraX,
                req.CameraY,
                req.ViewportWidth,
                req.ViewportHeight,
                req.WorldWidth,
                req.WorldHeight
            );

            EventManager.EmitCallback(req.CallbackId, viewportData);
        }
    }

    private ViewportDataBinary GetViewportDataBinary(float cameraX, float cameraY, float viewportWidth, float viewportHeight, float? worldWidth = null, float? worldHeight = null)
    {
        var tileGrid = World.GetTilesInArea(
            cameraX,
            cameraY,
            viewportWidth,
            viewportHeight,
            worldWidth,
            worldHeight
        );

        var width = (ushort)tileGrid.GetLength(0);
        var height = (ushort)tileGrid.GetLength(1);

        _logger.LogDebug($"Creating viewport data: {width}x{height}");

        // Calculate total size: 4 bytes header + tiles
        var dataSize = 4 + (width * height);
        var buffer = _arrayPool.Rent(dataSize);

        // Write dimensions
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(0..2), width);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(2..4), height);

        _logger.LogDebug($"Header bytes: [{string.Join(", ", buffer.AsSpan(0..4).ToArray())}]");

        // Write tile data
        var i = 4;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                buffer[i] = tileGrid[x, y];
                if (x < 5 && y < 5)
                {
                    _logger.LogDebug($"Tile({x},{y}): material={buffer[i]}");
                }
                i++;
            }
        }

        return new ViewportDataBinary
        {
            EncodedData = new Memory<byte>(buffer, 0, dataSize)
        };
    }

    public override void Init()
    {
        // Nothing to initialize yet
    }

    public override void Destroy()
    {
        EventManager.UnregisterListener<ViewportRequest>(this);
    }
}