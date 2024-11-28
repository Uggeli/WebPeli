using System.Net.WebSockets;
using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Util;

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
    public required Position TopLeft { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public Guid CallbackId { get; init; }
    public required WebSocket Socket { get; init; }
    public required Guid ConnectionId { get; init; }
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
    public required int EntityId { get; init; }
    public required Position FromPosition { get; init; }  // Current position
    public required Position ToPosition { get; init; }    // Target position
    public required MovementType MovementType { get; init; }
    public required Guid CallbackId { get; init; }
}

public readonly record struct PathfindingRequest : IEvent
{
    public required int EntityId { get; init; }
    // public required float StartX { get; init; }
    // public required float StartY { get; init; }
    // public required float TargetX { get; init; }
    // public required float TargetY { get; init; }
    public required Position FromPosition { get; init; }
    public required Position ToPosition { get; init; }
    public Guid CallbackId { get; init; }
}

// MovementManager handles this event and finds path for entity
// Then puts in to movement queue and starts moving entity down the path with movement type
// In world coordinates (world_size * chunk_size)
public readonly record struct FindPathAndMoveEntity : IEvent
{
    public required int EntityId { get; init; }
    // public required int StartX { get; init; }
    // public required int StartY { get; init; }
    // public required int TargetX { get; init; }
    // public required int TargetY { get; init; }
    public required Position FromPosition { get; init; }
    public required Position ToPosition { get; init; }
    public required EntityAction MovementType { get; init; }
}


// Raised when entity has moved to target position
public readonly record struct EntityMovementSucceeded : IEvent
{
    public required int EntityId { get; init; }
}

// Raised when entity has failed to move to target position, Typically world.cs would raise this
public readonly record struct EntityMovementFailed : IEvent
{
    public required int EntityId { get; init; }
}


public enum SystemType : byte
{
    MetabolismSystem,
    MovementSystem,
    RenderingSystem,
    AiSystem,
}

public readonly record struct RegisterToSystem : IEvent
{
    public  int EntityId { get; init; }
    public SystemType SystemType { get; init; } // Type of the system to register to
}

public readonly record struct UnregisterFromSystem : IEvent
{
    public  int EntityId { get; init; }
    public SystemType SystemType { get; init; } // Type of the system to unregister from
}

public readonly record struct DeathEvent : IEvent
{
    public int EntityId { get; init; }
    // Entitys life is over, but is that the end? stay tuned for the next episode of Dragon Ball Z 
    
}
public enum ThresholdSeverity : byte
{
    Mild,
    Severe,
    Critical
}

public enum ThresholdType : byte
{
    Hunger,
    Thirst,
    Fatigue
}
public readonly record struct EntityThresholdReached : IEvent
{
    public required int EntityId { get; init; }
    public required ThresholdType ThresholdType { get; init; }
    public required ThresholdSeverity Severity { get; init; }
}

public record ConsumeFood(int EntityId, byte Amount) : IEvent;
public record ConsumeDrink(int EntityId, byte Amount) : IEvent;
public record Rest(int EntityId, byte Amount) : IEvent;
public record CreateEntity : IEvent
{
    public required EntityCapabilities[] Capabilities { get; init; }
    public Position[]? Positions { get; init; }
}

public record RemoveEntity : IEvent
{
    public int EntityId { get; init; }
    // EntityManager catches this and removes the entity and sends
    // UnregisterFromSystem to all systems that entity has interfaces for
}



/// <summary>
/// What Entity is doing
/// </summary>
[Flags]
public enum EntityAction : int
{
    None = 0,
    Idle = 1 << 0,

    // Movement
    Walking = 1 << 1,
    Running = 1 << 2,
    Sneaking = 1 << 3,
    Jumping = 1 << 4,
    Climbing = 1 << 5,

    // Manipulation
    PickingUp = 1 << 6,
    Dropping = 1 << 7,
    Pushing = 1 << 8,
    Pulling = 1 << 9,
    Carrying = 1 << 10,

    // Metabolism
    Eating = 1 << 11,
    Drinking = 1 << 12,
    Resting = 1 << 13,
}

/// <summary>
/// What Entity is
/// </summary>

[Flags]
public enum EntityType : int
{
    None = 0,
    Living = 1 << 0,  // This thing breathes
    Resource = 1 << 1,  // This thing can be harvested
    Structure = 1 << 2,  // This thing is a building
}


