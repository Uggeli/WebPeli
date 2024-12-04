using WebPeli.GameEngine;

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
        // set => _currentSeason = value;
    }

    public static TimeOfDay CurrentTimeOfDay
    {
        get => _currentTimeOfDay;
        // set => _currentTimeOfDay = value;
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
        { Season.Spring, 90 },
        { Season.Summer, 90 },
        { Season.Autumn, 90 },
        { Season.Winter, 90 }
    };
    private int _currentTick = 0;
    static private int _currentDay = 0;
    static private int _currentYear = 0;
    private int _updateTick = 0;
    
    public static int CurrentDay
    {
        get => _currentDay;
        // set => _currentDay = value;
    }

    public static int CurrentYear
    {
        get => _currentYear;
        // set => _currentYear = value;
    }

    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);
        _updateTick++;
        
        if (_updateTick < Config.TicksToUpdateTimeOfDay)
            return;
        
        _updateTick = 0;
        _currentTick++;
        UpdateTimeOfDay();
    }

    private void UpdateTimeOfDay()
    {
        if (_currentTick < GetTimeOfDayLength(_currentTimeOfDay, _currentSeason))
            return;
        
        _currentTick = 0;
        _currentTimeOfDay++;
        _logger.LogInformation("Time of day changed to {0}", _currentTimeOfDay);
        
        if (_currentTimeOfDay <= TimeOfDay.Night)
            return;
            
        AdvanceToNextDay();
    }

    private void AdvanceToNextDay()
    {
        _currentTimeOfDay = TimeOfDay.Dawn;
        _currentDay++;
        
        if (_currentDay < SeasonLengths[_currentSeason])
            return;
            
        AdvanceToNextSeason();
    }

    private void AdvanceToNextSeason()
    {
        _currentDay = 0;
        _currentSeason++;
        _logger.LogInformation("Season changed to {0}", _currentSeason);
        if (_currentSeason > Season.Winter)
        {
            _currentSeason = Season.Spring;
            _currentYear++;
        }
    }

    public override void Destroy()
    {
        
    }

    public override void HandleMessage(IEvent evt)
    {
        
    }

    public override void Init()
    {
        
    }
}