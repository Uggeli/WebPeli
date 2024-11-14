using System.Numerics;
using WebPeli.GameEngine.EntitySystem;

namespace WebPeli.GameEngine.Managers;

// TODO: move static stuff to World class
public enum Direction : byte
{
    Up = 0,
    Right = 1,
    Down = 2,
    Left = 3,
    None = 4,
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

    private class MovementData(int currentX, int currentY, (int, int)[] path, MovementType movementType)
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
            CurrentMoveIndex += 1;
            return nextMove;
        }

        public void UpdateCurrentPosition()
        {
            var nextMove = Path[CurrentMoveIndex];
            CurrentX = nextMove.Item1;
            CurrentY = nextMove.Item2;
        }
    }

    private readonly Dictionary<Guid, MovementData> _movingEntities = [];

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
        base.Update(deltaTime);

        // Later: add deltaTime to moving entities, now just use crude loop limiter
        if (_tickCounter++ >= 5)
        {
            _tickCounter = 0;
            MoveEntities(deltaTime);
        }

    }

    private void HandlePathAndMove(FindPathAndMoveEntity request)
    {
        var path = World.FindPath(request.StartX, request.StartY, request.TargetX, request.TargetY);
        if (path == null || path.Length == 0)
        {
            // No path found
            return;
        }

        var entityId = request.EntityId;
        var movementData = new MovementData(
            request.StartX, request.StartY, path, request.MovementType
        );
        _movingEntities[entityId] = movementData;
    }

    private void HandleEntityMove(MoveEntityRequest request)
    {
        // TODO
    }

    private void MoveEntities(double deltaTime)
    {
        foreach (var (entityId, movementData) in _movingEntities)
        {
            var (nextX, nextY) = movementData.GetNextMove();

            System.Console.WriteLine($"Moving entity {entityId}: from {movementData.CurrentX}, {movementData.CurrentY} to {nextX}, {nextY}");
            System.Console.WriteLine($"Path goal: {movementData.Path.Last().Item1}, {movementData.Path.Last().Item2}");
            if (nextX == movementData.CurrentX && nextY == movementData.CurrentY)
            {
                System.Console.WriteLine("Entity has reached the end of the path");
                System.Console.WriteLine($"Path length: {movementData.Path.Length}");
                foreach (var (x, y) in movementData.Path)
                {
                    System.Console.WriteLine($"Path: {x}, {y}");
                }
                // Entity has reached the end of the path
                _movingEntities.Remove(entityId);
                World.SetEntityState(entityId, new EntityState
                {
                    Position = new EntityPosition(
                        movementData.CurrentX,
                        movementData.CurrentY
                    ),
                    Current = CurrentAction.Idle
                });
            }
            else
            {
                movementData.UpdateCurrentPosition();
                World.UpdateEntityPosition(entityId, new EntityPosition(
                    nextX, nextY
                ));
            }
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

    private static bool ValidateEntityPositions(IEnumerable<EntityPosition> positions, out Dictionary<(int, int), List<EntityPosition>> chunkPositions)
    {
        chunkPositions = [];

        foreach (var pos in positions)
        {
            var (chunkX, chunkY, localX, localY) =
                Util.CoordinateSystem.WorldToChunkAndLocal(pos.X, pos.Y);

            // Validate chunk exists
            if (chunkX < 0 || chunkX >= Config.WORLD_SIZE ||
                chunkY < 0 || chunkY >= Config.WORLD_SIZE)
                return false;

            var key = (chunkX, chunkY);
            if (!chunkPositions.TryGetValue(key, out var positionList))
            {
                positionList = [];
                chunkPositions[key] = positionList;
            }
            positionList.Add(new EntityPosition(localX, localY));
        }

        return true;
    }

    public bool AddEntity(Guid entity, IEnumerable<EntityPosition> worldPositions)
    {
        if (!ValidateEntityPositions(worldPositions, out var chunkPositions))
            return false;

        // Try to add to all relevant chunks
        foreach (var ((chunkX, chunkY), positions) in chunkPositions)
        {
       
        }
        return true;
    }

    public bool RemoveEntity(Guid entityId)
    {
        return World.RemoveEntity(entityId);
    }
}


