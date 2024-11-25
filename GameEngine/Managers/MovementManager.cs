using System.Collections.Concurrent;
using System.Numerics;
using WebPeli.GameEngine.EntitySystem;

namespace WebPeli.GameEngine.Managers;

// TODO: move static stuff to World class
public enum Direction : byte
{
    Up = 0,
    North = Up,
    Right = 1,
    East = Right,
    Down = 2,
    South = Down,
    Left = 3,
    West = Left,
    None = 4
}
// Movement system:
// Ai checks available moves and then selects move it wants to perform and sends MoveEntityRequest to MovementManager
// MovementManager checks if the move is valid and then moves the entity and sends event to AnimationManager
// Move takes time and entity can't move again until the move is completed
public enum MovementType : byte
{
    Walk = 0,
    Run = 1,
    Sneak = 2,
    jump = 3,
    climb = 4,
    swim = 5,
}

public class MovementManager : BaseManager
{

    internal class MovementData(Position current, Position[] path, MovementType movementType)
    {
        public Position CurrentPosition {get; set;} = current;
        public Position[] Path { get; init; } = path;
        public MovementType MovementType { get; init; } = movementType;
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
        Parallel.ForEach(EventQueue, HandleMessage);
        EventQueue.Clear();
        // Later: add deltaTime to moving entities, now just use crude loop limiter
        if (_tickCounter++ >= 1)
        {
            _tickCounter = 0;
            MoveEntities(deltaTime);
        }

    }

    private void HandlePathAndMove(FindPathAndMoveEntity request)
    {
        // System.Console.WriteLine("Handling path and move");
        int EntityId = request.EntityId;
        Position fromPosition = request.FromPosition;
        Position toPosition = request.ToPosition;

        var path = World.GetPath(fromPosition, toPosition);
        if (path == null || path.Length == 0)
        {
            EventManager.Emit(new EntityMovementFailed{EntityId = EntityId});

            if (Config.DebugPathfinding)
            {
                Console.WriteLine($"Failed to find path from {fromPosition} to {toPosition}");
            }
            return;
        }




        var movementData = new MovementData(fromPosition, path, request.MovementType);
        var oldState = World.GetEntityState(EntityId);
        EntityState newState;
        if (oldState == null)
        {
            newState = new EntityState([fromPosition], CurrentAction.Moving, fromPosition.LookAt(path[1]));           
        }
        else
        {
            oldState.Position = [fromPosition];
            oldState.CurrentAction = CurrentAction.Moving;
            oldState.Direction = fromPosition.LookAt(path[1]);
            newState = oldState;
        }

        World.SetEntityState(EntityId, newState);
        _movingEntities.TryAdd(EntityId, movementData);
    }

    private void HandleEntityMove(MoveEntityRequest request)
    {
        // TODO, Teleport entity to new position
    }

    private void MoveEntities(double deltaTime)
    {
        if (Config.DebugPathfinding)
        {
            // Console.WriteLine("Moving entities");
            Console.WriteLine($"Moving {_movingEntities.Count} entities");
        }


        ConcurrentBag<int> toRemove = [];
        Parallel.ForEach(_movingEntities, kvp =>
        {
            var entityId = kvp.Key;
            var movementData = kvp.Value;
            var nextMove = movementData.GetNextMove();
            var currentPos = movementData.CurrentPosition;
            movementData.UpdateCurrentPosition();


            if (Config.DebugPathfinding)
            {
                Console.WriteLine($"Entity {entityId} moving to {nextMove} from {movementData.CurrentPosition}");
                Console.WriteLine($"Entity {entityId} path : {movementData.CurrentMoveIndex}/{movementData.Path.Length}");
            }

            if (nextMove == currentPos)
            {
                // Entity has reached target position
                if (Config.DebugPathfinding)
                {
                    Console.WriteLine($"Entity {entityId} reached target position");
                }
                World.SetEntityState(entityId, new EntityState([nextMove], CurrentAction.Idle, nextMove.LookAt(movementData.CurrentPosition)));
                toRemove.Add(entityId);
                EventManager.Emit(new EntityMovementSucceeded{EntityId = entityId});
                return;
            }
            World.MoveEntity(entityId, [nextMove]);

            if (Config.DebugPathfinding)
            {
                Console.WriteLine($"Entity {entityId} moved to {nextMove} from {movementData.CurrentPosition}");
            }
        });

        foreach (var entityId in toRemove)
        {
            _movingEntities.TryRemove(entityId, out _);
        }
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


