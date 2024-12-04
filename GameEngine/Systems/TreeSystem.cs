using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Util;
using WebPeli.GameEngine.World;
using WebPeli.GameEngine.World.WorldData;

namespace WebPeli.GameEngine.Systems;

// 

public class TreeSystem(ILogger<TreeSystem> logger) : BaseManager
{
    private readonly ILogger<TreeSystem> _logger = logger;
    public override void Destroy()
    {
        throw new NotImplementedException();
    }

    public override void HandleMessage(IEvent evt)
    {
        throw new NotImplementedException();
    }

    public override void Init()
    {
        throw new NotImplementedException();
    }
}


public interface ITree {} // Marker interface for trees
// Statemachine
// Use Plantstatus enum to determine what the tree is doing and what it should do next or what it can do next