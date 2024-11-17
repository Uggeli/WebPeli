using System.Collections.Concurrent;
namespace WebPeli.GameEngine.Util;

public static class IDManager
{
    private static ConcurrentBag<int> _entityIds = [];
    private static ConcurrentBag<int> _zoneIds = [];
    public static int GetEntityId()
    {
        if (_entityIds.TryTake(out int id))
        {
            return id;
        }
        return _entityIds.Count;
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
        return _zoneIds.Count;
    }

    public static void ReturnZoneId(int id)
    {
        _zoneIds.Add(id);
    }
}




// long paths tend to be incorrect in long run and long paths take long to run 