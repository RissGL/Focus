namespace WpfApp1.Services;

public enum Locale
{
    EN,
    ZH
}

/// <summary>
/// Manages locale switching. Fires event when locale changes so UI can refresh.
/// </summary>
public static class LocaleManager
{
    private static readonly Dictionary<Locale, Dictionary<string, string>> Strings;

    public static Locale Current { get; private set; } = Locale.EN;
    public static event Action? LocaleChanged;

    static LocaleManager()
    {
        Strings = new Dictionary<Locale, Dictionary<string, string>>
        {
            [Locale.EN] = EnStrings(),
            [Locale.ZH] = ZhStrings()
        };
    }

    public static string Get(string key)
    {
        if (Strings[Current].TryGetValue(key, out var value))
            return value;
        return $"[{key}]";
    }

    public static void SetLocale(Locale locale)
    {
        if (Current == locale) return;
        Current = locale;
        LocaleChanged?.Invoke();
    }

    public static void Toggle()
    {
        SetLocale(Current == Locale.EN ? Locale.ZH : Locale.EN);
    }

    private static Dictionary<string, string> EnStrings() => new()
    {
        ["App.Title"] = "Focus Mode",
        ["Header.Ready"] = "Ready to focus",
        ["Header.Active"] = "Focusing...",
        ["Header.Stats"] = "Sessions completed: {0}\nTotal focus time: {1}h {2}m",
        ["Header.Level"] = "Lv.{0} {1}",
        ["Timer.Remaining"] = "{0} remaining",
        ["Timer.Complete"] = "Time's up! Session complete.",
        ["Status.NotMonitoring"] = "Not monitoring — click Start to begin",
        ["Status.Active"] = "Active — {0} violations so far",
        ["Status.Violations"] = "{0} violations",
        ["Status.BlockedApp"] = "Blocked: {0}.exe — closing...",
        ["Status.BlockedUrl"] = "Blocked URL: {0} — closing tab...",
        ["Btn.Start"] = "▶ Start Focus",
        ["Btn.Stop"] = "⏹ Stop",
        ["Whitelist.Apps"] = "Application Whitelist",
        ["Whitelist.Urls"] = "URL Whitelist",
        ["Btn.AddApp"] = "+ Add App",
        ["Btn.Remove"] = "- Remove",
        ["Btn.AddUrl"] = "+ Add",
        ["Settings.Duration"] = "Focus Duration:",
        ["Settings.DurationUnit"] = "min",
        ["Settings.EnforceUrl"] = "Enforce URL whitelist for browsers",
        ["Settings.AutoStart"] = "Start with Windows",
        ["Settings.Info"] = "Checks active window every 1.5 seconds. Only enforces when focus mode is active.",
        ["Dialog.AddApp.Title"] = "Add Application",
        ["Dialog.AddApp.Message"] = "Enter the process name (e.g., 'notepad' for Notepad.exe):",
        ["Dialog.Duplicate"] = "'{0}' is already in the whitelist.",
        ["Dialog.AlreadyExists"] = "Already Exists",
        ["Dialog.UrlRequired"] = "Please enter a URL pattern (e.g., github.com or bilibili.com/video/BV).",
        ["Dialog.InputRequired"] = "Input Required",
        ["Dialog.InvalidUrl"] = "Could not extract a valid URL pattern from the input.",
        ["Dialog.InvalidURL"] = "Invalid URL",
        ["Dialog.NoApps"] = "No enabled apps to remove.",
        ["Dialog.Info"] = "Info",
        ["Dialog.Remove.Title"] = "Remove App from Whitelist",
        ["Dialog.Cancel"] = "Cancel",
        ["Dialog.OK"] = "OK",
        ["Dialog.RemoveBtn"] = "Remove",
        ["Browser.Indicator"] = " — browser",
        ["Todo.Title"] = "Tasks",
        ["Radar.Title"] = "Stats",
        ["Abilities.Title"] = "Abilities",
        ["Tooltip.OpenChrome"] = "Open in Chrome",
        ["Tooltip.Browser"] = "Browser: URL whitelist applies",
        ["Tooltip.Remove"] = "Remove",
        ["Tooltip.DeleteTask"] = "Delete task",
    };

    private static Dictionary<string, string> ZhStrings() => new()
    {
        ["App.Title"] = "专注模式",
        ["Header.Ready"] = "准备就绪",
        ["Header.Active"] = "● 专注中...",
        ["Header.Stats"] = "已完成：{0} 次\n总专注时长：{1} 小时 {2} 分钟",
        ["Header.Level"] = "Lv.{0} {1}",
        ["Timer.Remaining"] = "剩余 {0}",
        ["Timer.Complete"] = "时间到！会话完成。",
        ["Status.NotMonitoring"] = "未监控 — 点击开始专注",
        ["Status.Active"] = "监控中 — {0} 次违规",
        ["Status.Violations"] = "{0} 次违规",
        ["Status.BlockedApp"] = "拦截：{0}.exe — 正在关闭...",
        ["Status.BlockedUrl"] = "拦截网址：{0} — 正在关闭标签页...",
        ["Btn.Start"] = "▶ 开始专注",
        ["Btn.Stop"] = "⏹ 停止",
        ["Whitelist.Apps"] = "应用白名单",
        ["Whitelist.Urls"] = "网址白名单",
        ["Btn.AddApp"] = "+ 添加应用",
        ["Btn.Remove"] = "- 移除",
        ["Btn.AddUrl"] = "+ 添加",
        ["Settings.Duration"] = "专注时长：",
        ["Settings.DurationUnit"] = "分钟",
        ["Settings.EnforceUrl"] = "浏览器启用网址白名单",
        ["Settings.AutoStart"] = "开机自动启动",
        ["Settings.Info"] = "每 1.5 秒检查一次活动窗口，仅在专注模式时生效。",
        ["Dialog.AddApp.Title"] = "添加应用",
        ["Dialog.AddApp.Message"] = "输入进程名（如 'notepad' 对应 Notepad.exe）：",
        ["Dialog.Duplicate"] = "'{0}' 已在白名单中。",
        ["Dialog.AlreadyExists"] = "已存在",
        ["Dialog.UrlRequired"] = "请输入网址匹配规则（如 github.com 或 bilibili.com/video/BV）。",
        ["Dialog.InputRequired"] = "需要输入",
        ["Dialog.InvalidUrl"] = "无法从此输入中提取有效的网址规则。",
        ["Dialog.InvalidURL"] = "无效网址",
        ["Dialog.NoApps"] = "没有可移除的已启用应用。",
        ["Dialog.Info"] = "提示",
        ["Dialog.Remove.Title"] = "从白名单移除应用",
        ["Dialog.Cancel"] = "取消",
        ["Dialog.OK"] = "确定",
        ["Dialog.RemoveBtn"] = "移除",
        ["Browser.Indicator"] = " — 浏览器",
        ["Todo.Title"] = "任务列表",
        ["Radar.Title"] = "能力坐标",
        ["Abilities.Title"] = "属性管理",
        ["Tooltip.OpenChrome"] = "在 Chrome 中打开",
        ["Tooltip.Browser"] = "浏览器：网址白名单生效",
        ["Tooltip.Remove"] = "移除",
        ["Tooltip.DeleteTask"] = "删除任务",
    };
}
