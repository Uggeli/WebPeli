
using System.Collections.Concurrent;

namespace WebPeli.GameEngine.Managers;

public static class EventManager
{
    private static readonly ConcurrentDictionary<Type, ConcurrentBag<IListener>> Listeners = [];
    private static readonly ConcurrentDictionary<Guid, Delegate> TempListeners = [];  // Used for callbacks
    
    public static void RegisterListener<T>(IListener listener)
    {
        Listeners.AddOrUpdate(typeof(T), 
            new ConcurrentBag<IListener> { listener }, 
            (key, existing) => { existing.Add(listener); return existing; });
    }

    public static void UnregisterListener<T>(IListener listener)
    {
        if (Listeners.TryGetValue(typeof(T), out var listenerBag))
        {
            // ConcurrentBag doesn't support Remove, so we recreate without the listener
            // This is acceptable since unregistration is rare compared to emission
            var newBag = new ConcurrentBag<IListener>();
            foreach (var l in listenerBag)
            {
                if (l != listener)
                    newBag.Add(l);
            }
            Listeners.TryUpdate(typeof(T), newBag, listenerBag);
        }
    }

    public static void Emit<T>(T evt) where T : IEvent
    {
        if (Listeners.TryGetValue(typeof(T), out var listenerBag))
        {
            // Convert to array once for thread safety
            var listeners = listenerBag.ToArray();
            for (int i = 0; i < listeners.Length; i++)
            {
                listeners[i].OnMessage(evt);
            }
        }
    }

    public static void EmitPriority<T>(T evt) where T : IEvent
    {
        if (Listeners.TryGetValue(typeof(T), out var listenerBag))
        {
            // Convert to array once for thread safety
            var listeners = listenerBag.ToArray();
            for (int i = 0; i < listeners.Length; i++)
            {
                listeners[i].OnPriorityMessage(evt);
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