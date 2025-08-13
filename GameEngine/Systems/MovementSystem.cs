using System.Collections.Concurrent;
using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World;

namespace WebPeli.GameEngine.Systems;

// TODO: move static stuff to World class


public class MovementSystem : BaseManager
{

    internal class MovementData(Position current, Position[] path, EntityAction movementType)
    {
        public Position CurrentPosition {get; set;} = current;
        public Position[] Path { get; init; } = path;
        public EntityAction MovementType { get; init; } = movementType;
        public int CurrentMoveIndex { get; set; } = 1; // Skip first position, it's the current position

        public Position GetNextMove()
        {
            if (CurrentMoveIndex >= Path.Length)
            {
                return CurrentPosition;
            }
            var nextMove = Path[CurrentMoveIndex];
            return nextMove;
        }

        public void UpdateCurrentPosition()
        {
            var nextMove = Path[CurrentMoveIndex];
            CurrentPosition = nextMove;
            CurrentMoveIndex++;
            CurrentMoveIndex = Math.Min(CurrentMoveIndex, Path.Length - 1);
        }
    }

    private readonly ConcurrentDictionary<int, MovementData> _movingEntities = [];

    public override void Destroy()
    {
        EventManager.UnregisterListener<ChunkCreated>(this);
        EventManager.UnregisterListener<MoveEntityRequest>(this);
        EventManager.UnregisterListener<PathfindingRequest>(this);
        EventManager.UnregisterListener<RegisterToSystem>(this);
        EventManager.UnregisterListener<UnregisterFromSystem>(this);
        EventManager.UnregisterListener<FindPathAndMoveEntity>(this);
    }

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            // TODO:do we even need to register to movement system?, investigate!
            case RegisterToSystem registerToSystem:
                if (registerToSystem.SystemType.HasFlag(SystemType.MovementSystem))
                {

                }
                break;

            case UnregisterFromSystem unregisterFromSystem:
                if (unregisterFromSystem.SystemType.HasFlag(SystemType.MovementSystem))
                {
                    //
                }
                break;


            case MoveEntityRequest moveEntityRequest:
                HandleEntityMove(moveEntityRequest);
                break;
            case PathfindingRequest request:

                break;
            case FindPathAndMoveEntity findPathAndMoveEntity:
                HandlePathAndMove(findPathAndMoveEntity);
                break;
            default:
                break;
        }
    }
    private int _tickCounter = 0;
    public override void Update(double deltaTime)
    {
        var tick = Environment.TickCount;
        
        // Process events using the base class method (more efficient)
        base.Update(deltaTime);
        
        // Later: add deltaTime to moving entities, now just use crude loop limiter
        if (_tickCounter++ >= 1)
        {
            _tickCounter = 0;
            MoveEntities(deltaTime);
        }
        _lastUpdateTime = Environment.TickCount - tick;
    }

    private void HandlePathAndMove(FindPathAndMoveEntity request)
    {
        // System.Console.WriteLine("Handling path and move");
        int EntityId = request.EntityId;
        Position fromPosition = request.FromPosition;
        Position toPosition = request.ToPosition;

        var path = WorldApi.GetPath(fromPosition, toPosition);
        if (path == null || path.Length <= 1)
        {
            EventManager.Emit(new EntityMovementFailed{EntityId = EntityId});

            if (Config.DebugPathfinding)
            {
                Console.WriteLine($"Failed to find path from {fromPosition} to {toPosition}");
            }
            return;
        }
        var movementData = new MovementData(fromPosition, path, request.MovementType);
        _movingEntities.TryAdd(EntityId, movementData);
        WorldApi.SetEntityAction(EntityId, request.MovementType);
    }

    private void HandleEntityMove(MoveEntityRequest request)
    {
        // TODO, Teleport entity to new position
    }

    private void MoveEntities(double deltaTime)
    {
        if (Config.DebugPathfinding)
        {
            Console.WriteLine($"Moving {_movingEntities.Count} entities");
        }

        List<int> toRemove = [];
        foreach (var kvp in _movingEntities)
        {
            var entityId = kvp.Key;
            var movementData = kvp.Value;
            var nextMove = movementData.GetNextMove();
            var currentPos = movementData.CurrentPosition;
            movementData.UpdateCurrentPosition();

            if (Config.DebugPathfinding)
            {
                Console.WriteLine($"Entity {entityId} moving to {nextMove} from {currentPos}");
                Console.WriteLine($"Entity {entityId} path : {movementData.CurrentMoveIndex}/{movementData.Path.Length}");
            }

            if (nextMove == currentPos)
            {
            // Entity has reached target position
            if (Config.DebugPathfinding)
            {
                Console.WriteLine($"Entity {entityId} reached target position");
            }
            
            toRemove.Add(entityId);
            WorldApi.SetEntityAction(entityId, EntityAction.None);
            EventManager.Emit(new EntityMovementSucceeded{EntityId = entityId});
            continue;
            }

            if (!WorldApi.TryMoveEntity(entityId, [nextMove]))
            {
            // Entity could not move to next position
            if (Config.DebugPathfinding)
            {
                Console.WriteLine($"Entity {entityId} could not move to {nextMove} from {currentPos}");
            }
            toRemove.Add(entityId);
            WorldApi.SetEntityAction(entityId, EntityAction.None);
            EventManager.Emit(new EntityMovementFailed{EntityId = entityId});
            continue;
            }
            WorldApi.SetEntityFacing(entityId, currentPos.LookAt(nextMove));
            if (Config.DebugPathfinding)
            {
            Console.WriteLine($"Entity {entityId} moved to {nextMove} from {currentPos}");
            }
        }

        foreach (var entityId in toRemove)
        {
            _movingEntities.TryRemove(entityId, out _);
        }
    }

    private void ProcessEntityMovement()
    {
        
    }



    public override void Init()
    {
        EventManager.RegisterListener<ChunkCreated>(this);
        EventManager.RegisterListener<MoveEntityRequest>(this);
        EventManager.RegisterListener<PathfindingRequest>(this);
        EventManager.RegisterListener<RegisterToSystem>(this);
        EventManager.RegisterListener<UnregisterFromSystem>(this);
        EventManager.RegisterListener<FindPathAndMoveEntity>(this);
    }
}


