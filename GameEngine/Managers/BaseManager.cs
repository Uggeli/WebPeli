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
        
        // Convert to array to avoid concurrent modification issues
        var events = EventQueue.ToArray();
        EventQueue.Clear();
        
        // Process all events
        for (int i = 0; i < events.Length; i++)
        {
            HandleMessage(events[i]);
        }
        
        _lastUpdateTime = Environment.TickCount - tick;
    }
    
    public (string, int) GetUpdateTimes()
    {
        return (_name, _lastUpdateTime);
    }

    public int UpdateTime { get { return _lastUpdateTime; } }
}
