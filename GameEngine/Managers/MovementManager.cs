using System.Numerics;
using WebPeli.GameEngine.EntitySystem;

namespace WebPeli.GameEngine.Managers;

// TODO: move static stuff to World class
public enum Direction : byte
{
    Up = 0,
    Right = 1,
    Down = 2,
    Left = 3
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

    private record struct MovementData
    {
        public int ChunkX { get; init; }
        public int ChunkY { get; init; }
        public byte CurrentX { get; init; }
        public byte CurrentY { get; init; }
        public (byte, byte)[] Path { get; init; }
        public MovementType MovementType { get; init; }
        private int _currentMoveIndex = 0;

        public MovementData(int chunkX, int chunkY, byte currentX, byte currentY, (byte, byte)[] path, MovementType movementType)
        {
            ChunkX = chunkX;
            ChunkY = chunkY;
            CurrentX = currentX;
            CurrentY = currentY;
            Path = path;
            MovementType = movementType;
        }

        public (byte, byte) GetNextMove()
        {
            if (_currentMoveIndex >= Path.Length)
            {
                return (CurrentX, CurrentY);
            }
            var nextMove = Path.Length > 0 ? Path[_currentMoveIndex] : (CurrentX, CurrentY);
            _currentMoveIndex++;
            return nextMove;
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
            case MoveEntityRequest moveEntityRequest:
                HandleEntityMove(moveEntityRequest);
                break;
            case PathfindingRequest request:
                var path = GetPath(
                    request.StartX, request.StartY,
                    request.TargetX, request.TargetY
                );
                EventManager.EmitCallback(request.CallbackId, path);
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
        if (_tickCounter++  >= 60)
        {
            _tickCounter = 0;
            MoveEntities(deltaTime);
        }

    }

    private void HandlePathAndMove(FindPathAndMoveEntity request)
    {

        float startScreenX = request.StartX;
        float startScreenY = request.StartY;
        float endScreenX = request.TargetX;
        float endScreenY = request.TargetY;

        var path = GetPath(startScreenX, startScreenY, endScreenX, endScreenY);
        if (path.Length == 0) return;  // No path found
        var (startChunkX, startChunkY, startLocalX, startLocalY) = Util.CoordinateSystem.ScreenToLocal(startScreenX, startScreenY);
        var movementData = new MovementData(
            startChunkX, startChunkY, startLocalX, startLocalY,
            path,
            request.MovementType
        );
        // Add entity to moving entities, overwriting if already exists
        _movingEntities[request.EntityId] = movementData;
        World.SetEntityState(request.EntityId, new EntityState
        {
            Position = new Vector2(startScreenX, startScreenY),
            Current = CurrentAction.Moving
        });
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
            if (nextX == movementData.CurrentX && nextY == movementData.CurrentY)
            {
                // Entity has reached the end of the path
                _movingEntities.Remove(entityId);
                World.SetEntityState(entityId, new EntityState
                {
                    Position = new Vector2(
                        movementData.ChunkX * Config.CHUNK_SIZE + movementData.CurrentX,
                        movementData.ChunkY * Config.CHUNK_SIZE + movementData.CurrentY
                    ),
                    Current = CurrentAction.Idle
                });
            }
            else
            {
                // Move entity
                World.SetEntityState(entityId, new EntityState
                {
                    Position = new Vector2(
                        movementData.ChunkX * Config.CHUNK_SIZE + nextX,
                        movementData.ChunkY * Config.CHUNK_SIZE + nextY
                    ),
                    Current = CurrentAction.Moving
                });
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
            // if (!_chunks[chunkX, chunkY].AddEntity(entity, positions))
            var chunk = World.GetChunk(chunkX, chunkY);
            if (chunk == null || !chunk.AddEntity(entity, positions))
                return false;
        }
        return true;
    }

    public bool RemoveEntity(Guid entityId)
    {
        return World.RemoveEntity(entityId);
    }

    // Path is returned in Chunk coordinates, path contains only start chunk coordinates
    public (byte, byte)[] GetPath(float startScreenX, float startScreenY, float endScreenX, float endScreenY)
    {
        var (startChunkX, startChunkY, startLocalX, startLocalY) = Util.CoordinateSystem.ScreenToLocal(startScreenX, startScreenY);
        var (endChunkX, endChunkY, endLocalX, endLocalY) = Util.CoordinateSystem.ScreenToLocal(endScreenX, endScreenY);

        var startChunk = World.GetChunk(startChunkX, startChunkY);
        var endChunk = World.GetChunk(endChunkX, endChunkY);

        if (startChunk == null || endChunk == null) return [];
        if (startChunk == endChunk) return startChunk.GetPath(startLocalX, startLocalY, endLocalX, endLocalY);

        var interChunkPath = FindInterChunkPath(startChunkX, startChunkY, endChunkX, endChunkY);
        if (interChunkPath.Length > 0)
        {
            Chunk? next_chunk = World.GetChunk(interChunkPath[1].Item1, interChunkPath[1].Item2);
            if (next_chunk == null) return [];
            var connectionPoint = World.GetChunkConnectionPoint(startChunk, next_chunk, (interChunkPath[1].Item1 - startChunkX, interChunkPath[1].Item2 - startChunkY));
            if (connectionPoint == null) return [];
            return startChunk.GetPath(startLocalX, startLocalY, connectionPoint.Value.Item1, connectionPoint.Value.Item2);
        }
        return [];
    }

    private static Vector2[] ConvertPathToWorldCoordinates((byte, byte)[] path, byte chunkX, byte chunkY)
    {
         return path.Select(node => new Vector2(
        chunkX * Config.CHUNK_SIZE + node.Item1,
        chunkY * Config.CHUNK_SIZE + node.Item2
        )).ToArray();
    }

    private static Vector2[] ConvertPathToWorldCoordinates((byte, byte)[] path, int chunkX, int chunkY)
    {
         return path.Select(node => new Vector2(
        chunkX * Config.CHUNK_SIZE + node.Item1,
        chunkY * Config.CHUNK_SIZE + node.Item2
        )).ToArray();
    }

    static (int, int)[] FindInterChunkPath(int startChunkX, int startChunkY, int endChunkX, int endChunkY)
    {
        var startChunk = World.GetChunk(startChunkX, startChunkY);
        var endChunk = World.GetChunk(endChunkX, endChunkY);
        if (startChunk == null || endChunk == null)
        {
            return [];
        }

        var openSet = new PriorityQueue<(int, int), float>();
        var cameFrom = new Dictionary<(int, int), (int, int)>();

        var gScore = new Dictionary<(int, int), float>();
        var fScore = new Dictionary<(int, int), float>();

        openSet.Enqueue((startChunkX, startChunkY), 0);
        gScore[(startChunkX, startChunkY)] = 0;
        fScore[(startChunkX, startChunkY)] = Heuristic((startChunkX, startChunkY), (endChunkX, endChunkY));

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current == (endChunkX, endChunkY))
            {
                var path = new List<(int, int)>
                {
                    current
                };
                while (cameFrom.ContainsKey(current))
                {
                    current = cameFrom[current];
                    path.Add(current);
                }
                path.Reverse();
                return [.. path];
            }

            foreach (var neighbour in World.GetNeighboringChunks(current.Item1, current.Item2))
            {
                var tentativeGScore = gScore[current] + 1; // 1 is the distance between two nodes
                if (!gScore.TryGetValue(neighbour, out float value) || tentativeGScore < value)
                {
                    cameFrom[neighbour] = current;
                    value = tentativeGScore;
                    gScore[neighbour] = value;
                    fScore[neighbour] = gScore[neighbour] + Heuristic(neighbour, (endChunkX, endChunkY));
                    openSet.Enqueue(neighbour, fScore[neighbour]);
                }
            }
        }
        return [];
    }

    static float Heuristic((int, int) a, (int, int) b)
    {
        return MathF.Abs(a.Item1 - b.Item1) + MathF.Abs(a.Item2 - b.Item2); // Manhattan distance
    }

    static bool HandleCollisionCheck(int x, int y)
    {
        // X and Y are in world coordinates
        var (chunkX, chunkY, localX, localY) = Util.CoordinateSystem.WorldToChunkAndLocal(x, y);
        var chunk = World.GetChunk(chunkX, chunkY);
        if (chunk == null)
        {
            return false;
        }
        return chunk.IsTraversable(localX, localY);
    }

}


