using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using WpfApp1.Models;

namespace WpfApp1.Services;

public class WhitelistStore
{
    private static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FocusApp");

    private static readonly string AppsFile = Path.Combine(DataFolder, "app_whitelist.json");
    private static readonly string UrlsFile = Path.Combine(DataFolder, "url_whitelist.json");
    private static readonly string SettingsFile = Path.Combine(DataFolder, "settings.json");
    private static readonly string LocaleFile = Path.Combine(DataFolder, "locale.txt");
    private static readonly string TodoFile = Path.Combine(DataFolder, "todos.json");
    private static readonly string ArchiveFile = Path.Combine(DataFolder, "todos_archive.json");
    private static readonly string AbilitiesFile = Path.Combine(DataFolder, "abilities.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    static WhitelistStore()
    {
        try { Directory.CreateDirectory(DataFolder); } catch { }
    }

    public static string DataPath => DataFolder;

    public static ObservableCollection<AppWhitelistEntry> LoadAppWhitelist()
    {
        return LoadList<AppWhitelistEntry>(AppsFile) ?? GetDefaultApps();
    }

    public static ObservableCollection<UrlWhitelistEntry> LoadUrlWhitelist()
    {
        return LoadList<UrlWhitelistEntry>(UrlsFile) ?? GetDefaultUrls();
    }

    public static FocusSettings LoadSettings()
    {
        if (!File.Exists(SettingsFile)) return new FocusSettings();
        try
        {
            var json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<FocusSettings>(json, JsonOptions) ?? new FocusSettings();
        }
        catch { return new FocusSettings(); }
    }

    public static void SaveAppWhitelist(ObservableCollection<AppWhitelistEntry> apps)
    {
        SaveList(AppsFile, apps);
    }

    public static void SaveUrlWhitelist(ObservableCollection<UrlWhitelistEntry> urls)
    {
        SaveList(UrlsFile, urls);
    }

    public static ObservableCollection<TodoItem> LoadTodoList()
    {
        var todos = LoadList<TodoItem>(TodoFile) ?? new ObservableCollection<TodoItem>();
        ResetDailyTasks(todos);
        return todos;
    }

    private static void ResetDailyTasks(ObservableCollection<TodoItem> todos)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        foreach (var item in todos)
        {
            if (item.Type == TodoType.ShortTerm) continue;
            if (item.IsFinished) continue;
            if (item.LastResetDate != today)
            {
                item.IsCompleted = false;
                item.LastResetDate = today;
            }
        }
    }

    public static void SaveTodoList(ObservableCollection<TodoItem> todos)
    {
        SaveList(TodoFile, todos);
    }

    public static ObservableCollection<TodoItem> LoadArchive()
    {
        return LoadList<TodoItem>(ArchiveFile) ?? new ObservableCollection<TodoItem>();
    }

    public static void SaveArchive(ObservableCollection<TodoItem> archived)
    {
        SaveList(ArchiveFile, archived);
    }

    public static ObservableCollection<Ability> LoadAbilities()
    {
        return LoadList<Ability>(AbilitiesFile) ?? GetDefaultAbilities();
    }

    public static void SaveAbilities(ObservableCollection<Ability> abilities)
    {
        SaveList(AbilitiesFile, abilities);
    }

    private static ObservableCollection<Ability> GetDefaultAbilities()
    {
        return new ObservableCollection<Ability>
        {
            new() { Name = "Programming", Icon = "💻", Color = "#6366F1" },
            new() { Name = "Learning",    Icon = "📚", Color = "#10B981" },
            new() { Name = "Exercise",    Icon = "🏃", Color = "#F59E0B" },
            new() { Name = "Creation",    Icon = "🎨", Color = "#EF4444" },
            new() { Name = "Social",      Icon = "💬", Color = "#8B5CF6" },
        };
    }

    public static void SaveSettings(FocusSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFile, json);
        }
        catch { }
    }

    public static void SaveLocale(Locale locale)
    {
        try { File.WriteAllText(LocaleFile, locale == Locale.ZH ? "ZH" : "EN"); }
        catch { }
    }

    public static Locale LoadLocale()
    {
        try
        {
            if (File.Exists(LocaleFile))
                return File.ReadAllText(LocaleFile).Trim() == "ZH" ? Locale.ZH : Locale.EN;
        }
        catch { }
        return Locale.EN;
    }

    private static ObservableCollection<T>? LoadList<T>(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<T>>(json, JsonOptions);
            return list != null ? new ObservableCollection<T>(list) : null;
        }
        catch { return null; }
    }

    private static void SaveList<T>(string path, ObservableCollection<T> list)
    {
        try
        {
            var json = JsonSerializer.Serialize(list.ToList(), JsonOptions);
            File.WriteAllText(path, json);
        }
        catch { }
    }

    private static ObservableCollection<AppWhitelistEntry> GetDefaultApps()
    {
        return new ObservableCollection<AppWhitelistEntry>
        {
            new() { ProcessName = "devenv", DisplayName = "Visual Studio" },
            new() { ProcessName = "Code", DisplayName = "VS Code" },
            new() { ProcessName = "msedge", DisplayName = "Microsoft Edge", IsBrowser = true },
            new() { ProcessName = "chrome", DisplayName = "Google Chrome", IsBrowser = true },
            new() { ProcessName = "Notepad", DisplayName = "Notepad" },
            new() { ProcessName = "WpfApp1", DisplayName = "Focus App (this app)" },
            new() { ProcessName = "explorer", DisplayName = "File Explorer" },
            new() { ProcessName = "wt", DisplayName = "Windows Terminal" },
            new() { ProcessName = "WindowsTerminal", DisplayName = "Windows Terminal" },
        };
    }

    private static ObservableCollection<UrlWhitelistEntry> GetDefaultUrls()
    {
        return new ObservableCollection<UrlWhitelistEntry>
        {
            new() { Pattern = "github.com", Description = "GitHub" },
            new() { Pattern = "stackoverflow.com", Description = "Stack Overflow" },
            new() { Pattern = "learn.microsoft.com", Description = "Microsoft Learn" },
            new() { Pattern = "bilibili.com/video/BV", Description = "Bilibili BV Video" },
        };
    }
}
