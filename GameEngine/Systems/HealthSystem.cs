using System.Collections.Concurrent;
using System.Reflection.Metadata;
using WebPeli.GameEngine.Managers;

namespace WebPeli.GameEngine.Systems;

public class HealthSystem(ILogger<HealthSystem> logger) : BaseManager
{
    private readonly ConcurrentDictionary<int, HealthComponent> _healthComponents = [];
    private readonly ILogger<HealthSystem> _logger = logger;
    public override void Init()
    {
        EventManager.RegisterListener<DamageEvent>(this);
        EventManager.RegisterListener<HealEvent>(this);
        EventManager.RegisterListener<DeathEvent>(this);
        EventManager.RegisterListener<RegisterToSystem>(this);
        EventManager.RegisterListener<UnregisterFromSystem>(this);
        EventManager.RegisterListener<DayChangedEvent>(this);
    }

    public override void Destroy()
    {
        EventManager.UnregisterListener<DamageEvent>(this);
        EventManager.UnregisterListener<HealEvent>(this);
        EventManager.UnregisterListener<DeathEvent>(this);
        EventManager.UnregisterListener<RegisterToSystem>(this);
        EventManager.UnregisterListener<UnregisterFromSystem>(this);
        EventManager.UnregisterListener<DayChangedEvent>(this);
    }

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case DamageEvent damageEvent:
                HandleDamage(damageEvent);
                break;
            case HealEvent healEvent:
                HandleHeal(healEvent);
                break;
            case DeathEvent deathEvent:
                HandleDeath(deathEvent);
                break;
            case RegisterToSystem registerToSystem:
                // Handle register to system
                break;
            case UnregisterFromSystem unregisterFromSystem:
                // Handle unregister from system
                break;
            case DayChangedEvent:
                HandleDayChanged();
                break;
        }
    }

    private void HandleDayChanged()
    {
        foreach ((int entityID, _) in _healthComponents)
        {
            // Get the component first
            if (_healthComponents.TryGetValue(entityID, out var component))
            {
                var newHealth = component.Health + component.regenRate;
                newHealth = Math.Min(newHealth, component.MaxHealth);

                // Update the component and store it back
                component.Health = newHealth;
                _healthComponents[entityID] = component;
            }
        }
    }

    private void HandleDamage(DamageEvent evt)
    {
        // Handle damage
    }

    private void HandleHeal(HealEvent evt)
    {
        // Handle heal
    }

    private void HandleDeath(DeathEvent evt)
    {
        // Handle death
    }
}

public struct HealthComponent
{
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int regenRate { get; set; }  // Daily regen rate
}


public readonly record struct DamageEvent(int EntityId, int Damage) : IEvent;
public readonly record struct HealEvent(int EntityId, int Heal) : IEvent;