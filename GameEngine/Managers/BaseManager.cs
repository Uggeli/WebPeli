using System.Collections.Concurrent;

namespace WebPeli.GameEngine;

public abstract class BaseManager : IListener
{
    public ConcurrentBag<IEvent> EventQueue {get; set; } = [];
    protected int _lastUpdateTime = 0;
    private string _name = "";
    protected BaseManager()
    {
        _name = GetType().Name;
    }

    public abstract void Init();
    public abstract void Destroy();
    public abstract void HandleMessage(IEvent evt);
    public virtual void Update(double deltaTime)
    {
        var tick = Environment.TickCount;
        foreach (var evt in EventQueue)
        {
            HandleMessage(evt);
        }
        EventQueue.Clear();
        _lastUpdateTime = Environment.TickCount - tick;
    }
    
    public (string, int) GetUpdateTimes()
    {
        return (_name, _lastUpdateTime);
    }

    public int UpdateTime { get { return _lastUpdateTime; } }
}
