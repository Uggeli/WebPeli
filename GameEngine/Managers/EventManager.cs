
using System.Collections.Concurrent;

namespace WebPeli.GameEngine.Managers;

public static class EventManager
{
    private static readonly ConcurrentDictionary<Type, List<IListener>> Listeners = [];
    private static readonly ConcurrentDictionary<Guid, Delegate> TempListeners = [];  // Used for callbacks
    public static void RegisterListener<T>(IListener listener)
    {
        if (!Listeners.ContainsKey(typeof(T)))
        {
            Listeners[typeof(T)] = [];
        }
        Listeners[typeof(T)].Add(listener);
    }

    public static void UnregisterListener<T>(IListener listener)
    {
        if (Listeners.ContainsKey(typeof(T)))
        {
            Listeners[typeof(T)].Remove(listener);
        }
    }

    public static void Emit<T>(T evt) where T : IEvent
    {
        System.Console.WriteLine($"Emitting event {evt.GetType().Name}");
        if (Listeners.ContainsKey(typeof(T)))
        {
            foreach (var listener in Listeners[typeof(T)])
            {
                System.Console.WriteLine($"Sending event to {listener.GetType().Name}");
                listener.OnMessage(evt);
            }
        }
    }

    public static void EmitPriority<T>(T evt) where T : IEvent
    {
        if (Listeners.ContainsKey(typeof(T)))
        {
            foreach (var listener in Listeners[typeof(T)])
            {
                listener.OnPriorityMessage(evt);
            }
        }
    }

    public static Guid RegisterCallback(Delegate callback)
    {
        var guid = Guid.NewGuid();
        TempListeners[guid] = callback;
        return guid;
    }

    public static void UnregisterCallback(Guid guid)
    {
        TempListeners.TryRemove(guid, out _);
    }

    public static void EmitCallback(Guid guid, params object[] args)
    {
        if (TempListeners.TryGetValue(guid, out Delegate? value))
        {
            value.DynamicInvoke(args);
            UnregisterCallback(guid);
        }
    }
}