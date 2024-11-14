using System.Collections.Concurrent;
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

    private readonly ConcurrentDictionary<Guid, MovementData> _movingEntities = [];

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
        // Later: add deltaTime to moving entities, now just use crude loop limiter
        if (_tickCounter++ >= 5)
        {
            _tickCounter = 0;
            MoveEntities(deltaTime);
        }

    }

    private void HandlePathAndMove(FindPathAndMoveEntity request)
    {
        // NOTE: copying guid to local variable results to blank guid
        var path = World.FindPath(request.StartX, request.StartY, request.TargetX, request.TargetY);
        if (path == null || path.Length == 0)
        {
            // No path found
            return;
        }
        var movementData = new MovementData(
            request.StartX, request.StartY, path, request.MovementType
        );
        _movingEntities[request.EntityId] = movementData;
        World.SetEntityState(request.EntityId, new EntityState
        {
            Position = new EntityPosition(
                request.StartX, request.StartY
            ),
            Current = CurrentAction.Moving
        });
    }

    private void HandleEntityMove(MoveEntityRequest request)
    {
        // TODO
    }

    private void MoveEntities(double deltaTime)
    {   
        Parallel.ForEach(_movingEntities, kvp =>
        {
            var entityId = kvp.Key;
            var movementData = kvp.Value;
            var (nextX, nextY) = movementData.GetNextMove();

            if (nextX == movementData.CurrentX && nextY == movementData.CurrentY)
            {
                // Entity has reached the end of the path
                _movingEntities.TryRemove(entityId, out _);
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
                System.Console.WriteLine($"Moving entity {entityId} to {nextX}, {nextY}");
                World.UpdateEntityPosition(entityId, new EntityPosition(
                    nextX, nextY
                ));
            }
        });
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


