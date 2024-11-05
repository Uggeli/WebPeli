using WebPeli.GameEngine.Managers;

namespace WebPeli.GameEngine.EntitySystem;

public class EntityManager : BaseManager
{
    private static EntityManager? _instance;
    public static EntityManager Instance => _instance ??= new EntityManager();
    private EntityChunk[,] _chunks = new EntityChunk[Config.WORLD_SIZE, Config.WORLD_SIZE];
    private readonly Dictionary<Guid, (byte chunkX, byte chunkY)> _entities = [];  // for quick lookup

    
    public override void Destroy()
    {
        EventManager.UnregisterListener<ChunkCreated>(this);
        EventManager.UnregisterListener<MoveEntityRequest>(this);
    }

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case ChunkCreated chunkCreated:
                HandleChunkCreated(chunkCreated);
                break;
            case MoveEntityRequest moveEntityRequest:
                HandleEntityMove(moveEntityRequest);
                break;
        }
    }

    private void HandleEntityMove(MoveEntityRequest request)
    {
        // Get current and target chunks
        var (fromChunkX, fromChunkY, _, _) = 
            Util.CoordinateSystem.WorldToChunkAndLocal(request.FromPosition.X, request.FromPosition.Y);
        var (toChunkX, toChunkY, toLocalX, toLocalY) = 
            Util.CoordinateSystem.WorldToChunkAndLocal(request.ToPosition.X, request.ToPosition.Y);

        // Handle movement
        var fromChunk = _chunks[fromChunkX, fromChunkY];
        var entity = fromChunk.GetEntity(request.EntityId);
        
        if (entity == null) return; // Entity not found where expected

        // If moving to different chunk
        if (fromChunkX != toChunkX || fromChunkY != toChunkY)
        {
            var toChunk = _chunks[toChunkX, toChunkY];
            if (toChunk.AddEntity(entity, [new(toLocalX, toLocalY)]))
            {
                fromChunk.RemoveEntity(request.EntityId);
                _entities[request.EntityId] = (toChunkX, toChunkY);
            }
        }
        else // Same chunk movement
        {
            fromChunk.UpdateEntityPositions(request.EntityId, [new(toLocalX, toLocalY)]);
        }
    }

    private void HandleChunkCreated(ChunkCreated chunkCreated)
    {
        var chunk = new EntityChunk();
        _chunks[chunkCreated.X, chunkCreated.Y] = chunk;
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

    public bool AddEntity(IEntity entity, IEnumerable<EntityPosition> worldPositions)
    {
        if (!ValidateEntityPositions(worldPositions, out var chunkPositions))
            return false;

        // Try to add to all relevant chunks
        foreach (var ((chunkX, chunkY), positions) in chunkPositions)
        {
            if (!_chunks[chunkX, chunkY].AddEntity(entity, positions))
                return false;
        }

        return true;
    }

    public bool RemoveEntity(Guid entityId)
    {
        if (!_entities.TryGetValue(entityId, out var chunkPos))
            return false;

        var (chunkX, chunkY) = chunkPos;
        return _chunks[chunkX, chunkY].RemoveEntity(entityId);
    }

    public IEntity? GetEntity(Guid entityId)
    {
        if (!_entities.TryGetValue(entityId, out var chunkPos))
            return null;

        var (chunkX, chunkY) = chunkPos;
        return _chunks[chunkX, chunkY].GetEntity(entityId);
    }

    public Dictionary<Guid,IEntity> GetEntitiesWithInterface<T>(IEnumerable<Guid> EntityIds)
    {
        Dictionary<Guid,IEntity> entities = new();
        foreach (var entityId in EntityIds)
        {
            var entity = GetEntity(entityId);
            if (entity is T tEntity)
            {
                entities[entityId] = entity;
            }
        }
        return entities;
    }

    public IEnumerable<IEntity> GetEntitiesAt(EntityPosition position)
    {
        var (chunkX, chunkY, localX, localY) = 
            Util.CoordinateSystem.WorldToChunkAndLocal(position.X, position.Y);

        return _chunks[chunkX, chunkY].GetEntitiesAt(new EntityPosition(localX, localY));
    }

    public void UpdateEntity(Guid entityId, IEntity entity)
    {
        if (!_entities.TryGetValue(entityId, out var chunkPos))
            return;

        var (chunkX, chunkY) = chunkPos;
        _chunks[chunkX, chunkY].UpdateEntity(entityId, entity);
    }
}


