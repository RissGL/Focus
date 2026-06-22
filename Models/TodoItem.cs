namespace WpfApp1.Models;

public enum TodoType { ShortTerm, Daily, LongTerm }

public class TodoItem
{
    public string Text { get; set; } = "";
    public bool IsCompleted { get; set; }
    public TodoType Type { get; set; } = TodoType.ShortTerm;
    public bool IsFinished { get; set; } // LongTerm: permanently done
    public string LastResetDate { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Display helpers
    public string TypeIcon => Type switch
    {
        TodoType.Daily => "🔄",
        TodoType.LongTerm => "🎯",
        _ => ""
    };

    public string TypeLabel(bool zh) => Type switch
    {
        TodoType.ShortTerm => zh ? "短期" : "Short",
        TodoType.Daily => zh ? "每日" : "Daily",
        TodoType.LongTerm => zh ? "长期" : "Long",
        _ => ""
    };
}
