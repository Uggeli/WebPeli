using System.Numerics;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World;

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
            var EntityAction = WorldApi.GetEntityAction(entity);
            if (EntityAction != EntityAction.None) continue;

            // Move to random direction
            var currentEntityPosition = WorldApi.GetEntityPositions(entity)[0];

            var newPosX = Tools.Random.Next(0, Config.WORLD_SIZE * Config.CHUNK_SIZE);
            var newPosY = Tools.Random.Next(0, Config.WORLD_SIZE * Config.CHUNK_SIZE);

            var MovementEvent = new FindPathAndMoveEntity
            {
                EntityId = entity,
                FromPosition = currentEntityPosition,
                ToPosition = new Position(newPosX, newPosY),
                MovementType = EntityAction.Walking
            };

            EventManager.Emit(MovementEvent);
        }
    }
}