using WebPeli.GameEngine.Managers;

namespace WebPeli.GameEngine.EntitySystem;

public class EntityChunk
{
    public const int SIZE = Config.CHUNK_SIZE;
    private readonly Dictionary<Guid, IEntity> _entities = [];
    private readonly Dictionary<EntityPosition, HashSet<Guid>> _positionMap = [];
    private readonly Dictionary<Guid, HashSet<EntityPosition>> _entityPositions = [];
    public bool AddEntity(IEntity entity, IEnumerable<EntityPosition> positions)
    {
        if (HasCollision(positions)) return false;

        var positionsSet = new HashSet<EntityPosition>(positions);
        _entities[entity.Id] = entity;
        _entityPositions[entity.Id] = positionsSet;

        foreach (var pos in positions)
        {
            if (!_positionMap.TryGetValue(pos, out var entities))
            {
                entities = []; // Create new set
                _positionMap[pos] = entities;
            }
            entities.Add(entity.Id);
        }
        return true;
    }

    public bool RemoveEntity(Guid entityId)
    {
        if (!_entities.Remove(entityId)) return false;

        if (_entityPositions.TryGetValue(entityId, out var positions))
        {
            foreach (var pos in positions)
            {
                if (_positionMap.TryGetValue(pos, out var entities))
                {
                    entities.Remove(entityId);
                    if (entities.Count == 0)
                        _positionMap.Remove(pos);
                }
            }
            _entityPositions.Remove(entityId);
        }

        return true;
    }

    private static bool ValidatePositions(IEnumerable<EntityPosition> positions)
    {
        foreach (var pos in positions)
        {
            if (pos.X < 0 || pos.X >= SIZE || pos.Y < 0 || pos.Y >= SIZE)
            {
                return false;
            }
        }
        return true;
    }

    private static async Task<bool> CheckCollisionWithTerrain(IEnumerable<EntityPosition> positions)
    {
        var tasks = new List<TaskCompletionSource<bool>>();
        foreach (var pos in positions)
        {
            var tcs = new TaskCompletionSource<bool>();
            tasks.Add(tcs);
            var CallbackId = EventManager.RegisterCallback(delegate (bool collides) { tcs.SetResult(collides); });
            EventManager.EmitPriority(new TerrainCollisionRequest{ X = pos.X, Y = pos.Y, CallbackId = CallbackId });
        }
        await Task.WhenAll(tasks.Select(t => t.Task));
        // If any of the tasks returned true, there was a collision
        return tasks.Any(t => t.Task.Result);
    }

    private bool HasCollision(IEnumerable<EntityPosition> positions)
    {
        var terrainCollisions = CheckCollisionWithTerrain(positions).Result;
        return positions.Any(pos => 
            _positionMap.ContainsKey(pos) && 
            _positionMap[pos].Count > 0 && 
            terrainCollisions);
    }

    public IEntity? GetEntity(Guid entityId)
    {
        return _entities.TryGetValue(entityId, out var entity) ? entity : null;
    }

    public IEnumerable<IEntity> GetEntitiesAt(EntityPosition position)
    {
        if (_positionMap.TryGetValue(position, out var entityIds))
        {
            foreach (var id in entityIds)
            {
                if (_entities.TryGetValue(id, out var entity))
                    yield return entity;
            }
        }
    }

    public bool UpdateEntityPositions(Guid entityId, IEnumerable<EntityPosition> newPositions)
    {
        if (!_entities.ContainsKey(entityId)) return false;
        if (!ValidatePositions(newPositions)) return false;

        // Remove from old positions
        if (_entityPositions.TryGetValue(entityId, out var oldPositions))
        {
            foreach (var pos in oldPositions)
            {
                if (_positionMap.TryGetValue(pos, out var entities))
                {
                    entities.Remove(entityId);
                    if (entities.Count == 0)
                        _positionMap.Remove(pos);
                }
            }
        }

        // Add to new positions
        var positions = new HashSet<EntityPosition>(newPositions);
        _entityPositions[entityId] = positions;
        
        foreach (var pos in positions)
        {
            if (!_positionMap.TryGetValue(pos, out var entities))
            {
                entities = new HashSet<Guid>();
                _positionMap[pos] = entities;
            }
            entities.Add(entityId);
        }

        return true;
    }

    public IEnumerable<IEntity> GetEntities()
    {
        return _entities.Values;
    }

    public IEnumerable<IEntity> GetEntitiesWithInterface<T>() where T : IEntity
    {
        return _entities.Values.Where(e => e is T);
    }

    public void UpdateEntity(Guid entityId, IEntity entity)
    {
        _entities[entityId] = entity;
    }
}


