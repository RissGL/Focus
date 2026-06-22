using WpfApp1.Models;

namespace WpfApp1.Services;

public record Achievement(string Id, string Icon, string NameEn, string NameZh, string DescEn, string DescZh);

public static class AchievementService
{
    public static readonly Achievement[] All =
    {
        new("first_session", "🎯", "First Focus", "初次专注",
            "Complete your first focus session", "完成第一次专注"),
        new("streak_3", "🔥", "3-Day Streak", "连续3天",
            "3 consecutive days of focus", "连续3天专注"),
        new("streak_7", "💪", "7-Day Streak", "连续7天",
            "7 consecutive days of focus", "连续7天专注"),
        new("streak_30", "🌟", "30-Day Streak", "连续30天",
            "30 consecutive days of focus", "连续30天专注"),
        new("sessions_10", "📚", "10 Sessions", "10次专注",
            "Complete 10 focus sessions", "完成10次专注"),
        new("sessions_50", "💎", "50 Sessions", "50次专注",
            "Complete 50 focus sessions", "完成50次专注"),
        new("sessions_100", "🏆", "100 Sessions", "100次专注",
            "Complete 100 focus sessions", "完成100次专注"),
        new("hours_10", "⏰", "10 Hours", "10小时专注",
            "10 hours of total focus time", "累计专注10小时"),
        new("hours_50", "⚡", "50 Hours", "50小时专注",
            "50 hours of total focus time", "累计专注50小时"),
        new("hours_100", "👑", "100 Hours", "100小时专注",
            "100 hours of total focus time", "累计专注100小时"),
    };

    /// <summary>Check and return newly unlocked achievements. Updates the unlocked list in-place.</summary>
    public static List<Achievement> CheckAndUnlock(FocusSettings stats)
    {
        var unlocked = stats.UnlockedAchievements;
        var totalTime = stats.TotalFocusTime;

        var newOnes = new List<Achievement>();
        foreach (var a in All)
        {
            if (unlocked.Contains(a.Id)) continue;

            bool earned = a.Id switch
            {
                "first_session" => stats.TotalSessionsCompleted >= 1,
                "streak_3" => stats.CurrentStreak >= 3,
                "streak_7" => stats.CurrentStreak >= 7,
                "streak_30" => stats.CurrentStreak >= 30,
                "sessions_10" => stats.TotalSessionsCompleted >= 10,
                "sessions_50" => stats.TotalSessionsCompleted >= 50,
                "sessions_100" => stats.TotalSessionsCompleted >= 100,
                "hours_10" => totalTime.TotalHours >= 10,
                "hours_50" => totalTime.TotalHours >= 50,
                "hours_100" => totalTime.TotalHours >= 100,
                _ => false
            };

            if (earned)
            {
                unlocked.Add(a.Id);
                newOnes.Add(a);
            }
        }
        return newOnes;
    }

    public static string LevelName(int level, bool zh) => level switch
    {
        1 => zh ? "专注新手" : "Focus Novice",
        2 => zh ? "专注学徒" : "Focus Apprentice",
        3 => zh ? "专注达人" : "Focus Practitioner",
        4 => zh ? "专注专家" : "Focus Expert",
        5 => zh ? "专注大师" : "Focus Master",
        6 => zh ? "专注宗师" : "Focus Grandmaster",
        7 => zh ? "专注传奇" : "Focus Legend",
        _ => ""
    };
}
