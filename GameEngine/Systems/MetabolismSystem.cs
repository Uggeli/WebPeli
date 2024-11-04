using WebPeli.GameEngine.Managers;

namespace WebPeli.GameEngine.EntitySystem.Interfaces;

public class MetabolismSystem : BaseManager
{
    private List<Guid> _entities = [];
    private const byte NORMAL_MAX = 64;
    private const byte MILD_THRESHOLD = 65;    // hungry/thirsty/tired
    private const byte SEVERE_THRESHOLD = 129;  // very hungry/etc
    private const byte CRITICAL_THRESHOLD = 193; // starving/etc
    private const byte DEATH_THRESHOLD = 255;    // dead as a doornail, poor guy

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case RegisterToSystem reg when reg.SystemType == typeof(MetabolismSystem):
                _entities.Add(reg.EntityId);
                break;
            case UnregisterFromSystem unreg when unreg.SystemType == typeof(MetabolismSystem):
                _entities.Remove(unreg.EntityId);
                break;
            default:
                break;        
        }
    }

    public override void Init()
    {
        EventManager.RegisterListener<RegisterToSystem>(this);
        EventManager.RegisterListener<UnregisterFromSystem>(this);
    }

    public override void Destroy()
    {
        EventManager.UnregisterListener<RegisterToSystem>(this);
        EventManager.UnregisterListener<UnregisterFromSystem>(this);
    }

    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);  // Call base update to handle messages

        // Fetch all entities with metabolism from EntityManager
        var entitiesWithMetabolism = EntityManager.GetEntitiesWithComponent<MetabolismComponent>();

        foreach (var entity in entitiesWithMetabolism)
        {
            var metabolism = entity.GetComponent<MetabolismComponent>();

            // Update metabolism logic here
            // For example, decrease hunger, thirst, and energy levels over time
            metabolism.Hunger = Math.Min(metabolism.Hunger + 1, DEATH_THRESHOLD);
            metabolism.Thirst = Math.Min(metabolism.Thirst + 1, DEATH_THRESHOLD);
            metabolism.Energy = Math.Max(metabolism.Energy - 1, 0);

            // Check thresholds and apply effects
            if (metabolism.Hunger >= DEATH_THRESHOLD || metabolism.Thirst >= DEATH_THRESHOLD)
            {
            // Handle entity death
            // Later: Raise death event and Unregister entity from all systems
            // Entitymanager catches death event and raises unregister events for all involved systems
            }
            else if (metabolism.Hunger >= CRITICAL_THRESHOLD || metabolism.Thirst >= CRITICAL_THRESHOLD)
            {
            // Apply critical effects
            }
            else if (metabolism.Hunger >= SEVERE_THRESHOLD || metabolism.Thirst >= SEVERE_THRESHOLD)
            {
            // Apply severe effects
            }
            else if (metabolism.Hunger >= MILD_THRESHOLD || metabolism.Thirst >= MILD_THRESHOLD)
            {
            // Apply mild effects
            }
        }
    }
}