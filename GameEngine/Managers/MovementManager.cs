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

    private readonly record struct MovementData
    {
        public float FromX { get; init; }
        public float FromY { get; init; }
        public float ToX { get; init; }
        public float ToY { get; init; }
        public float Speed { get; init; } // How long it takes to complete a move
        public Direction Direction { get; init; }
        public MovementType MovementType { get; init; }
    }

    private readonly Dictionary<Guid, MovementData> _entities = [];

    public override void Destroy()
    {
        EventManager.UnregisterListener<ChunkCreated>(this);
        EventManager.UnregisterListener<MoveEntityRequest>(this);
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
            default:
                break;
        }
    }

    private static void HandleEntityMove(MoveEntityRequest request)
    {
        // Get current and target chunks
        var (fromChunkX, fromChunkY, _, _) =
            Util.CoordinateSystem.WorldToChunkAndLocal(request.FromPosition.X, request.FromPosition.Y);
        var (toChunkX, toChunkY, toLocalX, toLocalY) =
            Util.CoordinateSystem.WorldToChunkAndLocal(request.ToPosition.X, request.ToPosition.Y);

        // Handle movement
        var fromChunk = World.GetChunk(fromChunkX, fromChunkY);
        if (fromChunk == null) return; // Chunk not found where expected

        // If moving to different chunk
        if (fromChunkX != toChunkX || fromChunkY != toChunkY)
        {
            var toChunk = World.GetChunk(toChunkX, toChunkY);
            if (toChunk != null && toChunk.AddEntity(request.EntityId, [new(toLocalX, toLocalY)]))
            {
                fromChunk.RemoveEntity(request.EntityId);
            }
        }
        else // Same chunk movement
        {
            fromChunk.UpdateEntityPositions(request.EntityId, [new(toLocalX, toLocalY)]);
        }
    }

    public override void Init()
    {
        EventManager.RegisterListener<ChunkCreated>(this);
        EventManager.RegisterListener<MoveEntityRequest>(this);
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

    public (byte, byte)[] GetPath(float startScreenX, float startScreenY, float endScreenX, float endScreenY)
    {
        var (startChunkX, startChunkY, startLocalX, startLocalY) = Util.CoordinateSystem.ScreenToLocal(startScreenX, startScreenY);
        var (endChunkX, endChunkY, endLocalX, endLocalY) = Util.CoordinateSystem.ScreenToLocal(endScreenX, endScreenY);

        var startChunk = World.GetChunk(startChunkX, startChunkY);
        var endChunk = World.GetChunk(endChunkX, endChunkY);

        if (startChunk == null || endChunk == null)
        {
            return [];
        }
        if (startChunk == endChunk)
        {
            return startChunk.GetPath(startLocalX, startLocalY, endLocalX, endLocalY);
        }
        var interChunkPath = FindInterChunkPath(startChunkX, startChunkY, endChunkX, endChunkY);
        if (interChunkPath.Length > 0)
        {
            List<(byte, byte)> Path = [];
            Chunk? next_chunk = World.GetChunk(interChunkPath[1].Item1, interChunkPath[1].Item2);
            if (next_chunk == null)
            {
                return [.. Path];
            }
            var connectionPoint = World.GetChunkConnectionPoint(startChunk, next_chunk, (interChunkPath[1].Item1 - startChunkX, interChunkPath[1].Item2 - startChunkY));
            if (connectionPoint == null)
            {
                return [.. Path];
            }
            return startChunk.GetPath(startLocalX, startLocalY, connectionPoint.Value.Item1, connectionPoint.Value.Item2);
        }
        return [];
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


