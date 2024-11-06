using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using WebPeli.GameEngine.Managers;

namespace WebPeli.GameEngine.EntitySystem.Interfaces;

public class MetabolismSystem : BaseManager
{
    private Dictionary<Guid, int> _entities = [];
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
            case ConsumeFood food:
                HandleConsumeFood(food);
                break;

            case ConsumeDrink drink:
                HandleConsumeDrink(drink);
                break;

            case Rest rest:
                HandleRest(rest);
                break;
            case RegisterToSystem reg when reg.SystemType == SystemType.MetabolismSystem:
                _entities.Add(reg.EntityId, 0); // initialize to 0
                break;
            case UnregisterFromSystem unreg when unreg.SystemType == SystemType.MetabolismSystem:
                _entities.Remove(unreg.EntityId);
                break;
            default:
                break;        
        }
    }

    private void HandleConsumeFood(ConsumeFood food)
    {
        if (_entities.ContainsKey(food.EntityId))
        {
            int State = _entities[food.EntityId];
            
            _entities[food.EntityId] = State;
        }
    }

    private void HandleConsumeDrink(ConsumeDrink drink)
    {
        if (_entities.ContainsKey(drink.EntityId))
        {
            int State = _entities[drink.EntityId];
            
            _entities[drink.EntityId] = State;
        }
    }

    private void HandleRest(Rest rest)
    {
        if (_entities.ContainsKey(rest.EntityId))
        {
            int State = _entities[rest.EntityId];
            
            _entities[rest.EntityId] = State;
        }
    }

    public override void Init()
    {
        EventManager.RegisterListener<RegisterToSystem>(this);
        EventManager.RegisterListener<UnregisterFromSystem>(this);
        EventManager.RegisterListener<ConsumeFood>(this);
        EventManager.RegisterListener<ConsumeDrink>(this);
        EventManager.RegisterListener<Rest>(this);
    }

    public override void Destroy()
    {
        EventManager.UnregisterListener<RegisterToSystem>(this);
        EventManager.UnregisterListener<UnregisterFromSystem>(this);
        EventManager.UnregisterListener<ConsumeFood>(this);
        EventManager.UnregisterListener<ConsumeDrink>(this);
        EventManager.UnregisterListener<Rest>(this);
    }
    private int _tickcounter = 0;
    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);  // Call base update to handle messages
        if (++_tickcounter >= 60)  // Update every 60 ticks
        {
            _tickcounter = 0;
            foreach ((var id, _) in _entities)
            {
                int State = _entities[id];
                int hungerBits = State & HUNGER_MASK;
                int thirstBits = State & THIRST_MASK;
                int fatigueBits = State & FATIGUE_MASK;

                // Shift up for hunger/thirst (getting worse)
                hungerBits = (hungerBits << 1) & HUNGER_MASK;
                thirstBits = (thirstBits << 1) & THIRST_MASK;
                // Shift down for fatigue (recovering)
                fatigueBits = (fatigueBits >> 1) & FATIGUE_MASK;

                // Combine back
                State = hungerBits | thirstBits | fatigueBits;
                _entities[id] = State;

                EvaluateState(id, State);
            }
        }
    }

    private static void EvaluateState(Guid EntityId, int State)
    {
        // Check if entity is dead
        if ((State & (HUNGER_DEATH | THIRST_DEATH | FATIGUE_DEATH)) != 0)
        {
            EventManager.Emit(new DeathEvent{EntityId = EntityId});
            return;
        }


        // Test hunger
        if ((State & HUNGER_CRITICAL) != 0)
        {
            EventManager.Emit(new EntityThresholdReached{EntityId = EntityId, ThresholdType = ThresholdType.Hunger, Severity = ThresholdSeverity.Critical});
        }
        else if ((State & HUNGER_SEVERE) != 0)
        {
            EventManager.Emit(new EntityThresholdReached{EntityId = EntityId, ThresholdType = ThresholdType.Hunger, Severity = ThresholdSeverity.Severe});
        }
        else if ((State & HUNGER_MILD) != 0)
        {
            EventManager.Emit(new EntityThresholdReached{EntityId = EntityId, ThresholdType = ThresholdType.Hunger, Severity = ThresholdSeverity.Mild});
        }

        // Test thirst
        if ((State & THIRST_CRITICAL) != 0)
        {
            EventManager.Emit(new EntityThresholdReached{EntityId = EntityId, ThresholdType = ThresholdType.Thirst, Severity = ThresholdSeverity.Critical});
        }
        else if ((State & THIRST_SEVERE) != 0)
        {
            EventManager.Emit(new EntityThresholdReached{EntityId = EntityId, ThresholdType = ThresholdType.Thirst, Severity = ThresholdSeverity.Severe});
        }
        else if ((State & THIRST_MILD) != 0)
        {
            EventManager.Emit(new EntityThresholdReached{EntityId = EntityId, ThresholdType = ThresholdType.Thirst, Severity = ThresholdSeverity.Mild});
        }

        // Test fatigue
        if ((State & FATIGUE_CRITICAL) != 0)
        {
            EventManager.Emit(new EntityThresholdReached{EntityId = EntityId, ThresholdType = ThresholdType.Fatigue, Severity = ThresholdSeverity.Critical});
        }
        else if ((State & FATIGUE_SEVERE) != 0)
        {
            EventManager.Emit(new EntityThresholdReached{EntityId = EntityId, ThresholdType = ThresholdType.Fatigue, Severity = ThresholdSeverity.Severe});
        }
        else if ((State & FATIGUE_MILD) != 0)
        {
            EventManager.Emit(new EntityThresholdReached{EntityId = EntityId, ThresholdType = ThresholdType.Fatigue, Severity = ThresholdSeverity.Mild});
        }
    }
}