using System.Numerics;
using WebPeli.GameEngine.EntitySystem;
using WebPeli.GameEngine.Util;

namespace WebPeli.GameEngine.Managers;
//Placeholder for Ai stuff
public class AiManager : BaseManager
{
    List<int> _entities = [];
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
        foreach (var entity in _entities)
        {
            EntityState? state = World.GetEntityState(entity);
            if (state == null) continue;

            // Placeholder for AI logic

            // Calculate total world size in tiles
            int worldSizeInTiles = Config.WORLD_SIZE * Config.CHUNK_SIZE;

            // Generate random target within world bounds
            // Using floats since entity positions are in world coordinates
            
            var target_x = Tools.Random.Next(0, worldSizeInTiles - 1);
            var target_y = Tools.Random.Next(0, worldSizeInTiles - 1);

            var currentPos = state.Position.First();
            Position targetPos = new() { X = target_x, Y = target_y}; 

            // Emit pathfinding request with world coordinates
            EventManager.Emit(new FindPathAndMoveEntity
            {
                EntityId = entity,
                FromPosition = currentPos,
                ToPosition = targetPos,
                MovementType = MovementType.Walk
            });
        }
    }
}