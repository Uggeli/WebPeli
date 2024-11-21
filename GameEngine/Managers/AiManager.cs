using System.Numerics;
using WebPeli.GameEngine.EntitySystem;
using WebPeli.GameEngine.Util;

namespace WebPeli.GameEngine.Managers;
//Placeholder for Ai stuff
public class AiManager : BaseManager
{
    List<Guid> _entities = [];
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

    public override void HandleMessage(IEvent evt)
    {
        switch (evt)
        {
            case RegisterToSystem registerToSystem:
                HandleRegisterToSystem(registerToSystem);
                break;
            case UnregisterFromSystem unregisterFromSystem:
                HandleUnregisterFromSystem(unregisterFromSystem);
                break;
            default:
                break;
        }
    }

    private void HandleRegisterToSystem(RegisterToSystem registerToSystem)
    {
        if (registerToSystem.SystemType.HasFlag(SystemType.AiSystem))
            _entities.Add(registerToSystem.EntityId);
    }

    private void HandleUnregisterFromSystem(UnregisterFromSystem unregisterFromSystem)
    {
        if (unregisterFromSystem.SystemType.HasFlag(SystemType.AiSystem))
            _entities.Remove(unregisterFromSystem.EntityId);
    }

    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);
        var entity_count = 0;
        foreach (var entity in _entities)
        {
            EntityState? state = World.GetEntityState(entity);
            if (!state.HasValue || state.Value.Current != CurrentAction.Idle)
            {
                continue;
            }
            entity_count++;
            // Placeholder for AI logic

            // Calculate total world size in tiles
            int worldSizeInTiles = Config.WORLD_SIZE * Config.CHUNK_SIZE;

            // Generate random target within world bounds
            // Using floats since entity positions are in world coordinates
            Random random = new Random();
            var target_x = random.Next(0, worldSizeInTiles);
            var target_y = random.Next(0, worldSizeInTiles);

            // Emit pathfinding request with world coordinates
            EventManager.Emit(new FindPathAndMoveEntity
            {
                EntityId = entity,
                StartX = state.Value.Position.X,
                StartY = state.Value.Position.Y,
                TargetX = target_x,
                TargetY = target_y,
                MovementType = MovementType.Walk
            });
        }
    }
}