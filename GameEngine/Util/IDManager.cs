using System.Collections.Concurrent;
namespace WebPeli.GameEngine.Util;

public static class IDManager
{
    // TODO: this shit always retun 0
    private static int _lastEntityId = 0;
    private static int _lastZoneId = 0;
    private static ConcurrentBag<int> _entityIds = [];
    private static ConcurrentBag<int> _zoneIds = [];
    public static int GetEntityId()
    {
        if (_entityIds.TryTake(out int id))
        {
            return id;
        }
        if (_lastEntityId == int.MaxValue) throw new OverflowException("Entity ID pool exhausted");

        return Interlocked.Increment(ref _lastEntityId);
    }

    public static void ReturnEntityId(int id)
    {
        _entityIds.Add(id);
    }

    public static int GetZoneId()
    {
        if (_zoneIds.TryTake(out int id))
        {
            return id;
        }
        if (_lastZoneId == int.MaxValue) throw new OverflowException("Zone ID pool exhausted");
        return Interlocked.Increment(ref _lastZoneId);
    }

    public static void ReturnZoneId(int id)
    {
        _zoneIds.Add(id);
    }
}




// long paths tend to be incorrect in long run and long paths take long to run 