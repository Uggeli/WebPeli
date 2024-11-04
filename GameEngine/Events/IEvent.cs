using WebPeli.GameEngine.EntitySystem;
using WebPeli.GameEngine.Managers;

namespace WebPeli.GameEngine;

public interface IEvent { } // Marker interface

public record ViewportData
{
    required public byte[,] TileGrid { get; init; }  // Texture IDs
    required public Dictionary<(int x, int y), EntityRenderData> Entities { get; init; }
}
public record EntityRenderData
{
    required public byte TextureId { get; init; }
    required public byte Facing { get; init; }
    required public string CurrentAction { get; init; }
}
public record TextureRequest : IEvent
{
    required public byte TextureId { get; init; }
    required public Guid CallbackId { get; init; }
}
public record TextureData
{
    required public byte TextureId { get; init; }
    required public string TextureName { get; init; }
}

public record ViewportRequest : IEvent
{
    public float CameraX { get; init; }
    public float CameraY { get; init; }
    public float ViewportWidth { get; init; }
    public float ViewportHeight { get; init; }
    public float? WorldWidth { get; init; }
    public float? WorldHeight { get; init; }
    public Guid CallbackId { get; init; }
}

public readonly record struct TerrainCollisionRequest : IEvent
{
    public int X { get; init; }
    public int Y { get; init; }
    public required Guid CallbackId { get; init; }
}

public readonly record struct ChunkCreated : IEvent
{
    public readonly int X { get; init; }
    public readonly int Y { get; init; }
}

public readonly record struct MoveEntityRequest : IEvent 
{
    public required Guid EntityId { get; init; }
    public required EntityPosition FromPosition { get; init; }  // Current position
    public required EntityPosition ToPosition { get; init; }    // Target position
    public required Guid CallbackId { get; init; }
}

public readonly record struct PathfindingRequest : IEvent
{
    public required Guid EntityId { get; init; }
    public required EntityPosition FromPosition { get; init; }
    public required EntityPosition ToPosition { get; init; }
    public required Guid CallbackId { get; init; }
}