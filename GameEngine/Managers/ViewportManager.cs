using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World;
using WebPeli.Network;

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

public readonly record struct ViewportTileDataBinary
{
    public required Memory<byte> EncodedData { get; init; }
    public (byte Width, byte Height) GetDimensions() =>
        (EncodedData.Span[0], EncodedData.Span[1]);
}

public readonly record struct ViewportEntityDataBinary
{
    public required Memory<byte> EncodedData { get; init; }
}

// Specialized manager for handling viewport requests
public class ViewportManager : BaseManager
{
    public readonly ConcurrentDictionary<Guid, ViewportSubscription> _activeViewports = [];
    private readonly ArrayPool<byte> _arrayPool;
    private readonly ILogger<ViewportManager> _logger;

    public class ViewportSubscription
    {
        public required WebSocket Socket { get; set; }
        public Position TopLeft { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[]? LastTileUpdate { get; set; }
        public byte[]? LastEntityUpdate { get; set; }
        public byte[]? LastUpdate { get; set; }
    }

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
                "Viewport request for area at ({X}, {Y})",
                req.TopLeft.X, req.TopLeft.Y
            );

            try
            {
                // Create or update subscription
                var subscription = new ViewportSubscription
                {
                    Socket = req.Socket,
                    TopLeft = new Position { X = req.TopLeft.X, Y = req.TopLeft.Y },
                    Width = req.Width,
                    Height = req.Height
                };

                _activeViewports.AddOrUpdate(req.ConnectionId, subscription, (_, _) => subscription);

                // Send initial data
                var viewportData = GetViewportDataBinary(
                    subscription.TopLeft,
                    subscription.Width,
                    subscription.Height
                );

                subscription.LastUpdate = viewportData.EncodedData.ToArray();

                EventManager.EmitCallback(req.CallbackId, viewportData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing viewport request");
                throw;
            }
        }
    }

    private static bool HasChanged(byte[]? oldData, ReadOnlySpan<byte> newData)
    {
        if (oldData == null) return true;
        if (oldData.Length != newData.Length) return true;

        return !newData.SequenceEqual(oldData);
    }

    public void RemoveSubscription(Guid connectionId)
    {
        _activeViewports.TryRemove(connectionId, out _);
    }

    public override void Update(double deltaTime)
    {
        var tick = Environment.TickCount;
        base.Update(deltaTime);

        foreach (var kvp in _activeViewports)
        {
            var sub = kvp.Value;
            if (sub.Socket.State != WebSocketState.Open)
            {
                _activeViewports.TryRemove(kvp.Key, out _);
                continue;
            }

            try
            {
                var newTileData = GetViewportTileDataBinary(sub.TopLeft, sub.Width, sub.Height);
                var newEntityData = GetViewportEntityDataBinary(sub.TopLeft, sub.Width, sub.Height);

                // Send tile updates if changed
                if (HasChanged(sub.LastTileUpdate, newTileData.EncodedData.Span))
                {
                    sub.LastTileUpdate = newTileData.EncodedData.ToArray();
                    sub.Socket.SendAsync(
                        new ArraySegment<byte>(sub.LastTileUpdate),
                        WebSocketMessageType.Binary,
                        true,
                        CancellationToken.None);
                }

                // Send entity updates if changed
                if (HasChanged(sub.LastEntityUpdate, newEntityData.EncodedData.Span))
                {
                    sub.LastEntityUpdate = newEntityData.EncodedData.ToArray();
                    sub.Socket.SendAsync(
                        new ArraySegment<byte>(sub.LastEntityUpdate),
                        WebSocketMessageType.Binary,
                        true,
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending viewport update");
                _activeViewports.TryRemove(kvp.Key, out _);
            }
        }
        _lastUpdateTime = Environment.TickCount - tick;
    }

    private ViewportDataBinary GetViewportDataBinary(Position topLeft, int width, int height)
    {
        var tileGrid = WorldApi.GetTilesInArea(topLeft, width, height);
        var entities = WorldApi.GetEntitiesInArea(topLeft, width, height);

        // Calculate total entity count for buffer size
        int totalEntities = entities.Sum(kvp => kvp.Value.Length);

        // Calculate total size needed
        const int PROTOCOL_HEADER_SIZE = 3; // 1 byte type + 2 bytes length
        const int PAYLOAD_HEADER_SIZE = 2; // width + height (1 byte each)
        int tileDataSize = width * height * 3; // 3 bytes per tile
        int entityDataSize = totalEntities * 9; // 9 bytes per entity
        int totalSize = PROTOCOL_HEADER_SIZE + PAYLOAD_HEADER_SIZE + tileDataSize + entityDataSize;

        var buffer = _arrayPool.Rent(totalSize);
        var span = buffer.AsSpan(0, totalSize);

        // Write protocol header
        span[0] = (byte)MessageType.ViewportData;
        BinaryPrimitives.WriteUInt16LittleEndian(span[1..], (ushort)(totalSize - PROTOCOL_HEADER_SIZE));

        // Write payload header
        int offset = PROTOCOL_HEADER_SIZE;
        span[offset++] = (byte)width;
        span[offset++] = (byte)height;

        // Write tile data
        for (int i = 0; i < tileGrid.Length; i++)
        {
            span[offset++] = (byte)tileGrid[i].material;
            span[offset++] = (byte)tileGrid[i].surface;
            span[offset++] = (byte)tileGrid[i].props;
        }

        // Write entity data
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

    private ViewportTileDataBinary GetViewportTileDataBinary(Position topLeft, int width, int height)
    {
        var tileGrid = WorldApi.GetTileRenderData(topLeft, width, height);

        const int PROTOCOL_HEADER_SIZE = 3;
        const int PAYLOAD_HEADER_SIZE = 2; // width + height
        int tileDataSize = width * height * 2; // 2 bytes per tile (material + surface)
        int totalSize = PROTOCOL_HEADER_SIZE + PAYLOAD_HEADER_SIZE + tileDataSize;

        var buffer = _arrayPool.Rent(totalSize);
        var span = buffer.AsSpan(0, totalSize);

        span[0] = (byte)MessageType.TileData;
        BinaryPrimitives.WriteUInt16LittleEndian(span[1..], (ushort)(totalSize - PROTOCOL_HEADER_SIZE));

        int offset = PROTOCOL_HEADER_SIZE;
        span[offset++] = (byte)width;
        span[offset++] = (byte)height;

        foreach (var (material, surface) in tileGrid)
        {
            span[offset++] = (byte)material;
            span[offset++] = (byte)surface;
        }

        return new ViewportTileDataBinary { EncodedData = new Memory<byte>(buffer, 0, totalSize) };
    }

    private ViewportEntityDataBinary GetViewportEntityDataBinary(Position topLeft, int width, int height)
    {
        var entities = WorldApi.GetEntitiesInArea(topLeft, width, height);

        int totalEntities = entities.Sum(kvp => kvp.Value.Length);
        const int PROTOCOL_HEADER_SIZE = 3;
        int entityDataSize = totalEntities * 9; // 9 bytes per entity
        int totalSize = PROTOCOL_HEADER_SIZE + entityDataSize;

        var buffer = _arrayPool.Rent(totalSize);
        var span = buffer.AsSpan(0, totalSize);

        span[0] = (byte)MessageType.EntityData;
        BinaryPrimitives.WriteUInt16LittleEndian(span[1..], (ushort)(totalSize - PROTOCOL_HEADER_SIZE));

        int offset = PROTOCOL_HEADER_SIZE;
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

        return new ViewportEntityDataBinary { EncodedData = new Memory<byte>(buffer, 0, totalSize) };
    }
    public override void Init()
    {
        // Nothing to initialize yet
    }

    public override async void Destroy()
    {
        EventManager.UnregisterListener<ViewportRequest>(this);
        EventManager.UnregisterListener<ViewportRequest>(this);

        // Close all active WebSocket connections
        var closeTasks = new List<Task>();
        foreach (var subscription in _activeViewports.Values)
        {
            if (subscription.Socket.State == WebSocketState.Open)
            {
                closeTasks.Add(subscription.Socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Server shutting down",
                    CancellationToken.None));
            }
        }

        // Wait for all connections to close with a timeout
        if (closeTasks.Count > 0)
        {
            await Task.WhenAll(closeTasks).WaitAsync(TimeSpan.FromSeconds(5));
        }

        _activeViewports.Clear();
    }
}