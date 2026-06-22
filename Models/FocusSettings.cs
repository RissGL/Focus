namespace WpfApp1.Models;

public class FocusSettings
{
    public int FocusDurationMinutes { get; set; } = 25;
    public int MonitorIntervalMs { get; set; } = 1500;
    public bool EnforceUrlWhitelist { get; set; } = true;
    public bool AutoStart { get; set; }
    public bool IsDarkMode { get; set; }

    // Gamification stats (persisted)
    public int TotalSessionsCompleted { get; set; }
    public long TotalFocusTimeTicks { get; set; }
    public string LastSessionDate { get; set; } = "";
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public List<string> UnlockedAchievements { get; set; } = new();

    // Computed helpers
    public TimeSpan TotalFocusTime
    {
        get => TimeSpan.FromTicks(TotalFocusTimeTicks);
        set => TotalFocusTimeTicks = value.Ticks;
    }

    public int Level => TotalFocusTime.TotalHours switch
    {
        < 1 => 1,
        < 5 => 2,
        < 15 => 3,
        < 30 => 4,
        < 60 => 5,
        < 100 => 6,
        _ => 7
    };

    public TimeSpan NextLevelThreshold => Level switch
    {
        1 => TimeSpan.FromHours(1),
        2 => TimeSpan.FromHours(5),
        3 => TimeSpan.FromHours(15),
        4 => TimeSpan.FromHours(30),
        5 => TimeSpan.FromHours(60),
        6 => TimeSpan.FromHours(100),
        _ => TimeSpan.MaxValue
    };

    public double LevelProgress => Level >= 7 ? 1.0 : TotalFocusTime / NextLevelThreshold;
}
