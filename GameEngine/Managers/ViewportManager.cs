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


    public ViewportManager()
    {
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

    private ViewportDataBinary GetViewportDataBinary(
        float cameraX,
        float cameraY,
        float viewportWidth,
        float viewportHeight,
        float? worldWidth = null,
        float? worldHeight = null)
    {
        var tileGrid = World.GetTilesInArea(
            cameraX,
            cameraY,
            viewportWidth,
            viewportHeight,
            worldWidth,
            worldHeight
        );

        var entityGrid = World.GetEntitiesInArea(
            cameraX,
            cameraY,
            viewportWidth,
            viewportHeight,
            worldWidth,
            worldHeight
        );

        var width = (ushort)tileGrid.GetLength(0);
        var height = (ushort)tileGrid.GetLength(1);
        
        // Calculate total size: 4 bytes header + tiles + entities
        var dataSize = 4 + (width * height) + (width * height * 4); // 4 bytes per entity cell
        var buffer = _arrayPool.Rent(dataSize);

        // Write dimensions
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(0..2), width);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(2..4), height);

        // Write tile data
        var i = 4;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                buffer[i++] = tileGrid[x, y];
            }
        }

        // Write entity data (4 bytes per cell: count, action, rotation)
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var cell = entityGrid[x, y];
                buffer[i++] = (byte)cell.Count;
                buffer[i++] = (byte)cell.Action;
                // Convert rotation to 0-255 range
                buffer[i++] = (byte)((cell.Rotation / (2 * Math.PI)) * 255);
                buffer[i++] = 0; // Reserved for future use
            }
        }

        // Return data with cleanup callback
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