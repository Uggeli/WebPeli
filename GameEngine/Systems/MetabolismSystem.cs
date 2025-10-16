using WebPeli.GameEngine.Managers;

namespace WebPeli.GameEngine.Systems;

public class MetabolismSystem : BaseManager
{
    private Dictionary<int, int> _entities = [];
    // Threshold bits - could make these byte flags if we want to be explicit
    private const int HUNGER_MILD = 1 << 0;     // 0b_0000_0001
    private const int HUNGER_SEVERE = 1 << 1;     // 0b_0000_0010
    private const int HUNGER_CRITICAL = 1 << 2;     // 0b_0000_0100
    private const int HUNGER_DEATH = 1 << 3;     // 0b_0000_1000

    private const int THIRST_MILD = 1 << 4;     // 0b_0001_0000
    private const int THIRST_SEVERE = 1 << 5;     // 0b_0010_0000
    private const int THIRST_CRITICAL = 1 << 6;     // 0b_0100_0000
    private const int THIRST_DEATH = 1 << 7;     // 0b_1000_0000

    private const int FATIGUE_MILD = 1 << 8;     // And so on...
    private const int FATIGUE_SEVERE = 1 << 9;
    private const int FATIGUE_CRITICAL = 1 << 10;
    private const int FATIGUE_DEATH = 1 << 11;

    // Track which bits belong to which system for easy masking
    private const int HUNGER_MASK = HUNGER_MILD | HUNGER_SEVERE | HUNGER_CRITICAL | HUNGER_DEATH;
    private const int THIRST_MASK = THIRST_MILD | THIRST_SEVERE | THIRST_CRITICAL | THIRST_DEATH;
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
        if (_entities.TryGetValue(food.EntityId, out int State))
        {
            // Extract the current hunger state
            int hungerState = State & HUNGER_MASK;

            // Subtract the food amount from the hunger state
            hungerState -= food.Amount;

            // Ensure the hunger state does not go below zero
            if (hungerState < 0)
            {
                hungerState = 0;
            }

            // Combine the new hunger state back into the overall state
            int newState = (State & ~HUNGER_MASK) | (hungerState & HUNGER_MASK);
            _entities[food.EntityId] = newState;
        }
    }

    private void HandleConsumeDrink(ConsumeDrink drink)
    {
        if (_entities.TryGetValue(drink.EntityId, out int State))
        {
            int thirstState = State & THIRST_MASK;
            thirstState -= drink.Amount;
            if (thirstState < 0)
            {
                thirstState = 0;
            }
            int newState = (State & ~THIRST_MASK) | (thirstState & THIRST_MASK);
            _entities[drink.EntityId] = newState;
        }
    }

    private void HandleRest(Rest rest)
    {
        if (_entities.TryGetValue(rest.EntityId, out int State))
        {
            int fatigueState = State & FATIGUE_MASK;
            fatigueState -= rest.Amount;
            if (fatigueState < 0)
            {
                fatigueState = 0;
            }
            int newState = (State & ~FATIGUE_MASK) | (fatigueState & FATIGUE_MASK);
            _entities[rest.EntityId] = newState;
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
        // TODO: move update timer stuff to Config
        if (++_tickcounter >= 60)  // Update every 60 ticks
        {
            var tick = Environment.TickCount;
            _tickcounter = 0;
            
            // Process entities more efficiently using KeyValuePair enumeration
            var entityIds = new int[_entities.Count];
            var entityStates = new int[_entities.Count];
            var index = 0;
            
            // Single enumeration to get all data
            foreach (var kvp in _entities)
            {
                entityIds[index] = kvp.Key;
                entityStates[index] = kvp.Value;
                index++;
            }
            
            // Process all entities without repeated dictionary lookups
            for (int i = 0; i < index; i++)
            {
                int id = entityIds[i];
                int state = entityStates[i];
                
                // Extract and process bits more efficiently
                int hungerBits = (state & HUNGER_MASK) << 1 & HUNGER_MASK;
                int thirstBits = (state & THIRST_MASK) << 1 & THIRST_MASK;
                int fatigueBits = (state & FATIGUE_MASK) >> 1 & FATIGUE_MASK;

                // Combine and update
                int newState = hungerBits | thirstBits | fatigueBits;
                _entities[id] = newState;

                EvaluateState(id, newState);
            }
            _lastUpdateTime = Environment.TickCount - tick;
        }
    }

    private static void EmitThresholdEvents(int entityId, int state, int criticalFlag, int severeFlag, int mildFlag, ThresholdType type)
    {
        if ((state & criticalFlag) != 0)
        {
            EventManager.Emit(new EntityThresholdReached { EntityId = entityId, ThresholdType = type, Severity = ThresholdSeverity.Critical });
        }
        else if ((state & severeFlag) != 0)
        {
            EventManager.Emit(new EntityThresholdReached { EntityId = entityId, ThresholdType = type, Severity = ThresholdSeverity.Severe });
        }
        else if ((state & mildFlag) != 0)
        {
            EventManager.Emit(new EntityThresholdReached { EntityId = entityId, ThresholdType = type, Severity = ThresholdSeverity.Mild });
        }
    }

    private static void EvaluateState(int EntityId, int State)
    {
        // Check if entity is dead
        if ((State & (HUNGER_DEATH | THIRST_DEATH | FATIGUE_DEATH)) != 0)
        {
            EventManager.Emit(new DeathEvent { EntityId = EntityId });
            return;
        }
        EmitThresholdEvents(EntityId, State, HUNGER_CRITICAL, HUNGER_SEVERE, HUNGER_MILD, ThresholdType.Hunger);
        EmitThresholdEvents(EntityId, State, THIRST_CRITICAL, THIRST_SEVERE, THIRST_MILD, ThresholdType.Thirst);
        EmitThresholdEvents(EntityId, State, FATIGUE_CRITICAL, FATIGUE_SEVERE, FATIGUE_MILD, ThresholdType.Fatigue);
    }
}