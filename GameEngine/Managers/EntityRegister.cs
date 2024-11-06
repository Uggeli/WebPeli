namespace WebPeli.GameEngine.Managers;

[Flags]
public enum EntityCapabilities : ushort
{
    // Registers what systems the entity uses
    None = 0,
    Metabolism = 1 << 0,
    Movement = 1 << 1,
    Render = 1 << 2,
    AiSystem = 1 << 3,  // Later, split this into more specific systems

}

public readonly record struct EntityRecord
{
    public readonly required Guid EntityId { get; init; }
    public readonly required EntityCapabilities Capabilities { get; init; }
}

public class EntityRegister : BaseManager
{
    // Handles entity creation and deletion
    private Dictionary<Guid, EntityRecord> _entities = [];


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

    private static void NotifySystems(Guid entityId, EntityCapabilities capabilities, bool remove = false)
    {
        foreach (var system in Enum.GetValues<EntityCapabilities>())
        {
            if (capabilities.HasFlag(system))
            {
                if (remove)
                {
                    EventManager.Emit(new UnregisterFromSystem{ EntityId=entityId, SystemType=(SystemType)system});
                }
                else
                {
                    EventManager.Emit(new RegisterToSystem{ EntityId=entityId, SystemType=(SystemType)system});
                }
            }
        }
    }

    void HandleCreateEntity(CreateEntity createEntity)
    {
        _entities.Add(createEntity.EntityId, new EntityRecord
        {
            EntityId = createEntity.EntityId,
            Capabilities = createEntity.Capabilities
        });
        NotifySystems(createEntity.EntityId, createEntity.Capabilities);
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
    }


}
