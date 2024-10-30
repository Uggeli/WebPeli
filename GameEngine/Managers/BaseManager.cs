namespace WebPeli.GameEngine;

public abstract class BaseManager : IListener
{
    public List<IEvent> EventQueue {get; set; } = [];
    public abstract void Init();
    public abstract void Destroy();
    public abstract void HandleMessage(IEvent evt);
    public void Update()
    {
        foreach (var evt in EventQueue)
        {
            HandleMessage(evt);
        }
        EventQueue.Clear();
    }
}
