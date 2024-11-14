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

// For teleporting entities, Enitypositions are in local coordinates
public readonly record struct MoveEntityRequest : IEvent 
{
    public required Guid EntityId { get; init; }
    public required EntityPosition FromPosition { get; init; }  // Current position
    public required EntityPosition ToPosition { get; init; }    // Target position
    public required MovementType MovementType { get; init; }
    public required Guid CallbackId { get; init; }
}

public readonly record struct PathfindingRequest : IEvent
{
    public required Guid EntityId { get; init; }
    public required float StartX { get; init; }
    public required float StartY { get; init; }
    public required float TargetX { get; init; }
    public required float TargetY { get; init; }
    public required Guid CallbackId { get; init; }
}

// MovementManager handles this event and finds path for entity
// Then puts in to movement queue and starts moving entity down the path with movement type
// In world coordinates (world_size * chunk_size)
public readonly record struct FindPathAndMoveEntity : IEvent
{
    public required Guid EntityId { get; init; }
    public required int StartX { get; init; }
    public required int StartY { get; init; }
    public required int TargetX { get; init; }
    public required int TargetY { get; init; }
    public required MovementType MovementType { get; init; }
}


public enum SystemType
{
    MetabolismSystem,
    MovementSystem,
    RenderingSystem,
    AiSystem,
}

public readonly record struct RegisterToSystem : IEvent
{
    public  Guid EntityId { get; init; }
    public SystemType SystemType { get; init; } // Type of the system to register to
}

public readonly record struct UnregisterFromSystem : IEvent
{
    public  Guid EntityId { get; init; }
    public SystemType SystemType { get; init; } // Type of the system to unregister from
}

public readonly record struct DeathEvent : IEvent
{
    public Guid EntityId { get; init; }
    // Entitys life is over, but is that the end? stay tuned for the next episode of Dragon Ball Z 
    
}
public enum ThresholdSeverity
{
    Mild,
    Severe,
    Critical
}

public enum ThresholdType
{
    Hunger,
    Thirst,
    Fatigue
}
public readonly record struct EntityThresholdReached : IEvent
{
    public required Guid EntityId { get; init; }
    public required ThresholdType ThresholdType { get; init; }
    public required ThresholdSeverity Severity { get; init; }
}

public record ConsumeFood(Guid EntityId, byte Amount) : IEvent;
public record ConsumeDrink(Guid EntityId, byte Amount) : IEvent;
public record Rest(Guid EntityId, byte Amount) : IEvent;
public record CreateEntity : IEvent
{
    public required EntityCapabilities[] Capabilities { get; init; }
    public Guid EntityId { get; init; }
}

public record RemoveEntity : IEvent
{
    public Guid EntityId { get; init; }
    // EntityManager catches this and removes the entity and sends
    // UnregisterFromSystem to all systems that entity has interfaces for
}