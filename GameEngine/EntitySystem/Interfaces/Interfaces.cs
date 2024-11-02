using WebPeli.GameEngine.Managers;

namespace WebPeli.GameEngine.EntitySystem.Interfaces;

public interface IMetabolism
{
    // 0-64, rest is for severerity levels like hungry, starving, etc.
    byte Hunger { get; set;} 
    byte Thirst { get; set;}
    byte Fatigue { get; set;} 
}
public interface IHealth
{
    byte Health { get; set;}
    byte MaxHealth { get; set;}
}

public readonly record struct RegisterToSystem : IEvent
{
    public  Guid EntityId { get; init; }
    public Type SystemType { get; init; } // Type of the system to register to
}

public readonly record struct UnregisterFromSystem : IEvent
{
    public  Guid EntityId { get; init; }
    public Type SystemType { get; init; } // Type of the system to unregister from
}


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
}