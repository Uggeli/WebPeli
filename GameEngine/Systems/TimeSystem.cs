using WebPeli.GameEngine;
using WebPeli.GameEngine.Managers;

namespace WebPeli.GameEngine.Systems;
public enum Season: byte
{
    Spring,
    Summer,
    Autumn,
    Winter
}

public enum TimeOfDay: byte
{
    Dawn,
    Noon,
    Evening,
    Dusk,
    Night
}


public class TimeSystem(ILogger<TimeSystem> logger) : BaseManager
{
    // Private static backing fields
    private static Season _currentSeason = Season.Spring;
    private static TimeOfDay _currentTimeOfDay = TimeOfDay.Noon;

    // Public static properties that allow modification
    public static Season CurrentSeason 
    { 
        get => _currentSeason;
    }

    public static TimeOfDay CurrentTimeOfDay
    {
        get => _currentTimeOfDay;
    }

    // Update your GetCurrentTime method to use the properties
    public static (Season, TimeOfDay, int day, int year) GetCurrentTime()
    {
        return (CurrentSeason, CurrentTimeOfDay, CurrentDay, CurrentYear);
    }
    private readonly ILogger<TimeSystem> _logger = logger;
    // Base time constants
    private const int BASE_DAWN_LENGTH = 60;
    private const int BASE_NOON_LENGTH = 120;
    private const int BASE_EVENING_LENGTH = 60;
    private const int BASE_DUSK_LENGTH = 30;
    private const int BASE_NIGHT_LENGTH = 60;

    // Seasonal adjustments (positive means longer)
    private const int WINTER_DAY_ADJUST = -20;
    private const int WINTER_NIGHT_ADJUST = 30;
    private const int SUMMER_DAY_ADJUST = 30;
    private const int SUMMER_NIGHT_ADJUST = -20;

    private int GetTimeOfDayLength(TimeOfDay timeOfDay, Season season)
    {
        int baseLength = timeOfDay switch
        {
            TimeOfDay.Dawn => BASE_DAWN_LENGTH,
            TimeOfDay.Noon => BASE_NOON_LENGTH,
            TimeOfDay.Evening => BASE_EVENING_LENGTH,
            TimeOfDay.Dusk => BASE_DUSK_LENGTH,
            TimeOfDay.Night => BASE_NIGHT_LENGTH,
            _ => throw new ArgumentException("Invalid time of day")
        };

        // Apply seasonal adjustments
        if (season == Season.Winter)
        {
            if (timeOfDay == TimeOfDay.Night)
                return baseLength + WINTER_NIGHT_ADJUST;
            return baseLength + WINTER_DAY_ADJUST;
        }
        
        if (season == Season.Summer)
        {
            if (timeOfDay == TimeOfDay.Night)
                return baseLength + SUMMER_NIGHT_ADJUST;
            return baseLength + SUMMER_DAY_ADJUST;
        }

        return baseLength; // Spring and Autumn unchanged
    }
    private readonly Dictionary<Season, byte> SeasonLengths = new()
    {
        { Season.Spring, Config.SpringLength },
        { Season.Summer, Config.SummerLength },
        { Season.Autumn, Config.AutumnLength },
        { Season.Winter, Config.WinterLength }
    };
    private int _currentTick = 0;
    static private int _currentDay = 0;
    static private int _currentYear = 0;
    private int _updateTick = 0;
    
    public static int CurrentDay
    {
        get => _currentDay;
    }

    public static int CurrentYear
    {
        get => _currentYear;
    }

    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);
        _updateTick++;
        // System.Console.WriteLine($"TimeSystem update tick {_updateTick}/{Config.TicksToUpdateTimeOfDay}");
        if (_updateTick <= Config.TicksToUpdateTimeOfDay)
            return;

        _updateTick = 0;
        _currentTick++;
        bool dayChanged = false;
        if (_currentTick >= GetTimeOfDayLength(_currentTimeOfDay, _currentSeason))
        {
            _currentTick = 0;
            _currentTimeOfDay = _currentTimeOfDay switch
            {
                TimeOfDay.Dawn => TimeOfDay.Noon,
                TimeOfDay.Noon => TimeOfDay.Evening,
                TimeOfDay.Evening => TimeOfDay.Dusk,
                TimeOfDay.Dusk => TimeOfDay.Night,
                TimeOfDay.Night => TimeOfDay.Dawn,
                _ => throw new ArgumentException("Invalid time of day")
            };
            EventManager.Emit(new TimeOfDayChangeEvent(_currentTimeOfDay));
            if (_currentTimeOfDay == TimeOfDay.Dawn)
                EventManager.Emit(new DayChangedEvent(_currentDay, _currentYear));
                dayChanged = true;
        }

        if (dayChanged)
        {
            _currentDay++;
            if (_currentDay % SeasonLengths[_currentSeason] == 0)
            {
                _currentSeason = _currentSeason switch
                {
                    Season.Spring => Season.Summer,
                    Season.Summer => Season.Autumn,
                    Season.Autumn => Season.Winter,
                    Season.Winter => Season.Spring,
                    _ => throw new ArgumentException("Invalid season")
                };
                EventManager.Emit(new SeasonChangeEvent(_currentSeason));
                if (_currentSeason == Season.Spring)
                    _currentYear++;
            }
        }
    }


    public override void Destroy()
    {
        EventManager.UnregisterListener<RequestTimeOfDayChangeEvent>(this);
        EventManager.UnregisterListener<RequestSeasonChangeEvent>(this);
    }

    public override void HandleMessage(IEvent evt)
    {
        switch(evt)
        {
            case RequestTimeOfDayChangeEvent requestTimeOfDayChangeEvent:
                HandleTimeOfDayChangeRequest(requestTimeOfDayChangeEvent);
                break;
            case RequestSeasonChangeEvent requestSeasonChangeEvent:
                HandleSeasonChangeRequest(requestSeasonChangeEvent);
                break;
        }
    }

    public override void Init()
    {
        EventManager.RegisterListener<RequestTimeOfDayChangeEvent>(this);
        EventManager.RegisterListener<RequestSeasonChangeEvent>(this);
    }

    private static void HandleSeasonChangeRequest(RequestSeasonChangeEvent evt)
    {
        _currentSeason = evt.NewSeason;
        EventManager.Emit(new SeasonChangeEvent(_currentSeason));
    }

    private static void HandleTimeOfDayChangeRequest(RequestTimeOfDayChangeEvent evt)
    {
        _currentTimeOfDay = evt.NewTimeOfDay;
        EventManager.Emit(new TimeOfDayChangeEvent(_currentTimeOfDay));
    }
}

public record struct DayChangedEvent(int day, int year) : IEvent;
public record struct TimeOfDayChangeEvent(TimeOfDay NewTimeOfDay) : IEvent;
public record struct SeasonChangeEvent(Season NewSeason) : IEvent;
public record struct RequestTimeOfDayChangeEvent(TimeOfDay NewTimeOfDay) : IEvent;
public record struct RequestSeasonChangeEvent(Season NewSeason) : IEvent;

