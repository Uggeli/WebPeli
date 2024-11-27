using System.Buffers;
using System.Buffers.Binary;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World;

namespace WebPeli.GameEngine.Managers;

// Network-ready version of viewport data
public readonly record struct ViewportDataBinary
{
    public required Memory<byte> EncodedData { get; init; }

    // Helper to get dimensions from encoded data
    public (byte Width, byte Height) GetDimensions()
    {
        return (EncodedData.Span[0], EncodedData.Span[1]);
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
            _logger.LogDebug(
                "Received viewport request for area at ({X:F2}, {Y:F2})",
                req.TopLeft.X, req.TopLeft.Y
            );

            try
            {
                var viewportData = GetViewportDataBinary(
                    req.TopLeft, req.Width, req.Height
                );
                EventManager.EmitCallback(req.CallbackId, viewportData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing viewport request");
                throw;
            }
        }
    }

    private ViewportDataBinary GetViewportDataBinary(Position topLeft, int width, int height)
    {
        var tileGrid = WorldApi.GetTilesInArea(topLeft, width, height);
        var entities = WorldApi.GetEntitiesInArea(topLeft, width, height);

        // Calculate total entity count for buffer size
        int totalEntities = entities.Sum(kvp => kvp.Value.Length);

        // Calculate total size needed
        int headerSize = 2; // width + height (1 byte each)
        int tileDataSize = width * height * 3; // 3 bytes per tile
        int entityDataSize = totalEntities * 9; // 9 bytes per entity
        int totalSize = headerSize + tileDataSize + entityDataSize;

        var buffer = _arrayPool.Rent(totalSize);
        var span = buffer.AsSpan(0, totalSize);

        // Write header
        span[0] = (byte)width;
        span[1] = (byte)height;

        // Write tile data
        int offset = 2;
        for (int i = 0; i < tileGrid.Length; i++)
        {
            span[offset++] = tileGrid[i].material;
            span[offset++] = (byte)tileGrid[i].surface;
            span[offset++] = (byte)tileGrid[i].props;
        }

        // Message format:
        // [Width: 1 byte]
        // [Height: 1 byte]
        // [Tile Data: width*height*3]
        // [Entity Data: foreach entity]
        //   [X: 1 byte]
        //   [Y: 1 byte] 
        //   [EntityId: 4 bytes]
        //   [Action: 1 byte]
        //   [Type: 1 byte]
        //   [Direction: 1 byte]
        
        // Write entity data directly (no count needed)
        foreach (var (pos, entitiesAtPos) in entities)
        {
            foreach (var (entityId, action, type, direction) in entitiesAtPos)
            {
                byte relX = (byte)(pos.X - topLeft.X);
                byte relY = (byte)(pos.Y - topLeft.Y);
                span[offset++] = relX;
                span[offset++] = relY;
                BinaryPrimitives.WriteInt32LittleEndian(span[offset..], entityId);
                offset += 4;
                span[offset++] = (byte)action;
                span[offset++] = (byte)type;
                span[offset++] = (byte)direction;
            }
        }

        return new ViewportDataBinary
        {
            EncodedData = new Memory<byte>(buffer, 0, totalSize)
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