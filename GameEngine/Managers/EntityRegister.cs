using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World;

namespace WebPeli.GameEngine.Managers;

[Flags]
public enum EntityCapabilities : ushort
{
    None = 0,
    MetabolismSystem = 1 << 0,
    MovementSystem = 1 << 1,
    RenderingSystem = 1 << 2,
    AiSystem = 1 << 3,
}

public static class EntityCapabilitiesExtensions 
{
    public static SystemType ToSystemType(this EntityCapabilities capability) 
    {
        return capability switch 
        {
            EntityCapabilities.MetabolismSystem => SystemType.MetabolismSystem,
            EntityCapabilities.MovementSystem => SystemType.MovementSystem,
            EntityCapabilities.RenderingSystem => SystemType.RenderingSystem,
            EntityCapabilities.AiSystem => SystemType.AiSystem,
            _ => throw new ArgumentException($"Invalid capability: {capability}")
        };
    }
}

public readonly record struct EntityRecord
{
    public readonly required EntityCapabilities[] Capabilities { get; init; }
}

public class EntityRegister(ILogger<EntityRegister> logger) : BaseManager
{
    // Handles entity creation and deletion
    ILogger<EntityRegister> _logger = logger;
    private Dictionary<int, EntityRecord> _entities = [];
    public int EntityCount => _entities.Count;

    public override void Destroy()
    {
        EventManager.UnregisterListener<CreateEntity>(this);
        EventManager.UnregisterListener<DeathEvent>(this);
        EventManager.UnregisterListener<RemoveEntity>(this);
    }

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case CreateEntity createEntity:
                HandleCreateEntity(createEntity);
                break;
            case DeathEvent deathEvent:
                HandleDeathEvent(deathEvent);
                break;
            case RemoveEntity removeEntity:
                HandleRemoveEntity(removeEntity);
                break;
            default:
                break;
        }
    }

    public override void Init()
    {
        EventManager.RegisterListener<CreateEntity>(this);
        EventManager.RegisterListener<DeathEvent>(this);  // For Entity death, death does not mean removal
        EventManager.RegisterListener<RemoveEntity>(this); // For Removing Entity
    }

    private static void NotifySystems(int entityId, EntityCapabilities[] capabilities, bool remove = false)
    {
        foreach (var capability in capabilities)
        {
            if (remove)
            {
                EventManager.Emit(new UnregisterFromSystem { EntityId = entityId, SystemType = capability.ToSystemType() });
            }
            else
            {
                EventManager.Emit(new RegisterToSystem { EntityId = entityId, SystemType = capability.ToSystemType() });
            }
        }
    }

    void HandleCreateEntity(CreateEntity createEntity)
    {
        var newEntityID = Util.IDManager.GetEntityId();
        _entities.Add(newEntityID, new EntityRecord
        {
            Capabilities = createEntity.Capabilities
        });
        Position[]? positions = createEntity.Positions;
        if (positions != null)
        {
            WorldApi.AddEntity(newEntityID, positions);
        }
        else
        {
            if (!WorldApi.AddEntity(newEntityID))
            {
                IDManager.ReturnEntityId(newEntityID);
                _entities.Remove(newEntityID);
                return;
            }
        }
        NotifySystems(newEntityID, createEntity.Capabilities);
    }

    void HandleDeathEvent(DeathEvent deathevent)
    {
        // Dunno what to do here, maybe remove entity from all systems?, death is not the end
    }

    void HandleRemoveEntity(RemoveEntity removeEntity)
    {
        if (_entities.TryGetValue(removeEntity.EntityId, out var entity))
        {
            NotifySystems(removeEntity.EntityId, entity.Capabilities, true);
            _entities.Remove(removeEntity.EntityId);
        }
        Util.IDManager.ReturnEntityId(removeEntity.EntityId);       
    }

}
