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
                HandleRegisterToSystem(registerToSystem);
                break;
            case UnregisterFromSystem unregisterFromSystem:
                HandleUnregisterFromSystem(unregisterFromSystem);
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
                var newHealth = component.Health + component.RegenRate;
                newHealth = Math.Min(newHealth, component.MaxHealth);

                // Update the component and store it back
                component.Health = newHealth;
                _healthComponents[entityID] = component;
            }
        }
    }

    private void HandleRegisterToSystem(RegisterToSystem evt)
    {
        var systemData = evt.SystemData;
        if (systemData is not HealthComponent healthComponent)
        {
            _logger.LogError("Invalid system data");
            return;
        }

        _healthComponents[evt.EntityId] = new HealthComponent
        {
            Health = healthComponent.Health,
            MaxHealth = healthComponent.MaxHealth,
            RegenRate = healthComponent.RegenRate
        };
    }

    private void HandleUnregisterFromSystem(UnregisterFromSystem evt)
    {
        _healthComponents.TryRemove(evt.EntityId, out _);
    }

    private void HandleDamage(DamageEvent evt)
    {
        if (_healthComponents.TryGetValue(evt.EntityId, out var component))
        {
            var newHealth = component.Health - evt.Damage;
            newHealth = Math.Max(newHealth, 0);

            // Update the component and store it back
            component.Health = newHealth;
            _healthComponents[evt.EntityId] = component;
        }
    }

    private void HandleHeal(HealEvent evt)
    {
        if (_healthComponents.TryGetValue(evt.EntityId, out var component))
        {
            var newHealth = component.Health + evt.Heal;
            newHealth = Math.Min(newHealth, component.MaxHealth);

            // Update the component and store it back
            component.Health = newHealth;
            _healthComponents[evt.EntityId] = component;
        }
    }

    private void HandleDeath(DeathEvent evt)
    {
        _healthComponents.TryRemove(evt.EntityId, out _);
    }
}

public struct HealthComponent
{
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int RegenRate { get; set; }  // Daily regen rate
}


public readonly record struct DamageEvent(int EntityId, int Damage) : IEvent;
public readonly record struct HealEvent(int EntityId, int Heal) : IEvent;