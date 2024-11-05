using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using WebPeli.GameEngine.Managers;

namespace WebPeli.GameEngine.EntitySystem.Interfaces;

public class MetabolismSystem : BaseManager
{
    private List<Guid> _entities = [];
    // Threshold bits - could make these byte flags if we want to be explicit
    private const int HUNGER_MILD     = 1 << 0;     // 0b_0000_0001
    private const int HUNGER_SEVERE   = 1 << 1;     // 0b_0000_0010
    private const int HUNGER_CRITICAL = 1 << 2;     // 0b_0000_0100
    private const int HUNGER_DEATH    = 1 << 3;     // 0b_0000_1000
    
    private const int THIRST_MILD     = 1 << 4;     // 0b_0001_0000
    private const int THIRST_SEVERE   = 1 << 5;     // 0b_0010_0000
    private const int THIRST_CRITICAL = 1 << 6;     // 0b_0100_0000
    private const int THIRST_DEATH    = 1 << 7;     // 0b_1000_0000

    private const int FATIGUE_MILD    = 1 << 8;     // And so on...
    private const int FATIGUE_SEVERE  = 1 << 9;
    private const int FATIGUE_CRITICAL = 1 << 10;
    private const int FATIGUE_DEATH   = 1 << 11;

    // Track which bits belong to which system for easy masking
    private const int HUNGER_MASK  = HUNGER_MILD | HUNGER_SEVERE | HUNGER_CRITICAL | HUNGER_DEATH;
    private const int THIRST_MASK  = THIRST_MILD | THIRST_SEVERE | THIRST_CRITICAL | THIRST_DEATH;
    private const int FATIGUE_MASK = FATIGUE_MILD | FATIGUE_SEVERE | FATIGUE_CRITICAL | FATIGUE_DEATH;

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
        EventManager.RegisterListener<ConsumeFood>(this);
        EventManager.RegisterListener<ConsumeDrink>(this);
        
    }

    public override void Destroy()
    {
        EventManager.UnregisterListener<RegisterToSystem>(this);
        EventManager.UnregisterListener<UnregisterFromSystem>(this);
    }
    private int _tickcounter = 0;
    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);  // Call base update to handle messages
        if (++_tickcounter >= 60)  // Update every 60 ticks
        {
            _tickcounter = 0;
            foreach ((Guid id, IEntity entity)  in EntityManager.Instance.GetEntitiesWithInterface<IMetabolism>(_entities))
            {
                if (entity is not IMetabolism meta) continue;
                
                int hungerBits = meta.State & HUNGER_MASK;
                int thirstBits = meta.State & THIRST_MASK;
                int fatigueBits = meta.State & FATIGUE_MASK;

                // Shift up for hunger/thirst (getting worse)
                hungerBits = (hungerBits << 1) & HUNGER_MASK;
                thirstBits = (thirstBits << 1) & THIRST_MASK;
                // Shift down for fatigue (recovering)
                fatigueBits = (fatigueBits >> 1) & FATIGUE_MASK;

                // Combine back
                meta.State = hungerBits | thirstBits | fatigueBits;

                switch (meta.State)
                {
                    case var state when IsAtDeathThreshold(state):
                        EventManager.Emit(new DeathEvent{EntityId = id});
                        break;
                    case var state when IsAtCriticalThreshold(state):
                        EventManager.Emit(new EntityThresholdReached{EntityId = id, Severity = ThresholdSeverity.Critical, ThresholdType = ThresholdType.Hunger});
                        break;
                    case var state when IsAtSevereThreshold(state):
                        EventManager.Emit(new EntityThresholdReached{EntityId = id, Severity = ThresholdSeverity.Severe, ThresholdType = ThresholdType.Hunger});
                        break;
                    case var state when IsAtMildThreshold(state):
                        EventManager.Emit(new EntityThresholdReached{EntityId = id, Severity = ThresholdSeverity.Mild, ThresholdType = ThresholdType.Hunger});
                        break;
                    default:
                        // Handle normal
                        break;
                }
                EntityManager.Instance.UpdateEntity(id, entity);
            }
        }
    }

    private static bool IsAtDeathThreshold(int state) => (state & (HUNGER_DEATH | THIRST_DEATH)) != 0;
    private static bool IsAtCriticalThreshold(int state) => (state & (HUNGER_CRITICAL | THIRST_CRITICAL)) != 0;
    private static bool IsAtSevereThreshold(int state) => (state & (HUNGER_SEVERE | THIRST_SEVERE)) != 0;
    private static bool IsAtMildThreshold(int state) => (state & (HUNGER_MILD | THIRST_MILD)) != 0;
    public bool IsHungerAtDeath(IMetabolism metabolism) => IsAtDeathThreshold(metabolism.State & HUNGER_MASK);
    public bool IsHungerAtCritical(IMetabolism metabolism) => IsAtCriticalThreshold(metabolism.State & HUNGER_MASK);
}