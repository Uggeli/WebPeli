using System.Collections.Concurrent;
using WebPeli.GameEngine.Managers;

namespace WebPeli.GameEngine.Systems;


/// <summary>
/// Harvest system is responsible for handling harvest requests, adding new harvest tables, adding new loot tables, entity death events, query harvest tables and query loot tables
/// </summary>
/// <param name="logger"></param>
public class HarvestSystem(ILogger<HarvestSystem> logger) : BaseManager
{
    private readonly ILogger<HarvestSystem> _logger = logger;
    private readonly ConcurrentDictionary<int, HarvestTable> _harvestTables = [];
    private readonly ConcurrentDictionary<int, LootTable> _lootTables = [];
    public override void Init()
    {
        EventManager.RegisterListener<HarvestRequestEvent>(this);
        EventManager.RegisterListener<AddNewHarvestTable>(this);
        EventManager.RegisterListener<AddNewLootTable>(this);
        EventManager.RegisterListener<DeathEvent>(this);
        EventManager.RegisterListener<QueryHarvestTable>(this);
        EventManager.RegisterListener<QueryLootTable>(this);
        EventManager.RegisterListener<RegisterToSystem>(this);
        EventManager.RegisterListener<UnregisterFromSystem>(this);
    }

    public override void Destroy()
    {
        EventManager.UnregisterListener<HarvestRequestEvent>(this);
        EventManager.UnregisterListener<AddNewHarvestTable>(this);
        EventManager.UnregisterListener<AddNewLootTable>(this);
        EventManager.UnregisterListener<DeathEvent>(this);
        EventManager.UnregisterListener<QueryHarvestTable>(this);
        EventManager.UnregisterListener<QueryLootTable>(this);
        EventManager.UnregisterListener<RegisterToSystem>(this);
        EventManager.UnregisterListener<UnregisterFromSystem>(this);
    }

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case HarvestRequestEvent harvestEvent:
                // Handle harvest request
                break;
            case AddNewHarvestTable addNewHarvestTable:
                // Handle adding new harvest table
                break;
            case AddNewLootTable addNewLootTable:
                // Handle adding new loot table
                break;
            case DeathEvent deathEvent:
                // Handle entity death
                break;
            case QueryHarvestTable queryHarvestTable:
                // Handle query harvest table
                break;
            case QueryLootTable queryLootTable:
                // Handle query loot table
                break;
        }
    }

    public void HandleHarvestRequest(HarvestRequestEvent evt)
    {
        // Handle harvest request
    }

    public void HandleAddNewHarvestTable(AddNewHarvestTable evt)
    {
        // Handle adding new harvest table
    }

    public void HandleAddNewLootTable(AddNewLootTable evt)
    {
        // Handle adding new loot table
    }

    public void HandleDeathEvent(DeathEvent evt)
    {
        // Handle entity death
    }

    public void HandleQueryHarvestTable(QueryHarvestTable evt)
    {
        // Handle query harvest table
    }

    public void HandleQueryLootTable(QueryLootTable evt)
    {
        // Handle query loot table
    }


}

public readonly record struct HarvestTable
{

} 

public readonly record struct LootTable  // Triggered on entity death event
{

}

// Events


public readonly record struct AddNewHarvestTable : IEvent
{
    public int EntityId { get; init; }
    public HarvestTable HarvestTable { get; init; }
}

public readonly record struct AddNewLootTable : IEvent
{
    public int EntityId { get; init; }
    public LootTable LootTable { get; init; }
}

public readonly record struct HarvestRequestEvent : IEvent
{
    public int EntityId { get; init; }
}


public readonly record struct QueryHarvestTable : IEvent
{
    public int EntityId { get; init; }
    public Guid CallbackId { get; init; }
}

public readonly record struct QueryLootTable : IEvent
{
    public int EntityId { get; init; }
    public Guid CallbackId { get; init; }
}

