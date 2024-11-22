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

    internal class MovementData(int currentX, int currentY, (int, int)[] path, MovementType movementType)
    {
        public int CurrentX { get; set; } = currentX;
        public int CurrentY { get; set; } = currentY;
        public (int, int)[] Path { get; init; } = path;
        public MovementType MovementType { get; init; } = movementType;
        private int CurrentMoveIndex { get; set; } = 1;

        public (int, int) GetNextMove()
        {
            if (CurrentMoveIndex >= Path.Length)
            {
                return (CurrentX, CurrentY);
            }
            var nextMove = Path[CurrentMoveIndex];
            CurrentMoveIndex++;
            return nextMove;
        }

        public void UpdateCurrentPosition()
        {
            var nextMove = Path[CurrentMoveIndex];
            CurrentX = nextMove.Item1;
            CurrentY = nextMove.Item2;
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
        if (_tickCounter++ >= 5)
        {
            _tickCounter = 0;
            MoveEntities(deltaTime);
        }

    }

    private void HandlePathAndMove(FindPathAndMoveEntity request)
    {
        System.Console.WriteLine("Handling path and move");

    }

    private void HandleEntityMove(MoveEntityRequest request)
    {
        // TODO, Teleport entity to new position
    }

    private void MoveEntities(double deltaTime)
    {
        List<int> toRemove = [];
        Parallel.ForEach(_movingEntities, kvp =>
        {
            
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


