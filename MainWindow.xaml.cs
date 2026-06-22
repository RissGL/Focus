using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using PathIO = System.IO.Path;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfApp1.Models;
using WpfApp1.Services;
using WpfApp1.Views;

namespace WpfApp1;

public class BoostSelectorItem
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public int BoostPoints { get; set; }
}

public partial class MainWindow : Window
{
    private ObservableCollection<AppWhitelistEntry> _appWhitelist = new();
    private ObservableCollection<UrlWhitelistEntry> _urlWhitelist = new();
    private ObservableCollection<TodoItem> _todoList = new();
    private ObservableCollection<TodoItem> _archivedTasks = new();
    private ObservableCollection<Ability> _abilities = new();
    private ObservableCollection<BoostSelectorItem> _boostItems = new();
    private TodoType _taskType = TodoType.ShortTerm;
    private FocusSessionManager? _session;
    private DispatcherTimer? _uiTimer;
    private FocusSettings _settings = new();
    private TrayIconService? _trayIcon;
    private bool _loading;

    public MainWindow()
    {
        InitializeComponent();

        var savedLocale = WhitelistStore.LoadLocale();
        LocaleManager.SetLocale(savedLocale);
        LocaleManager.LocaleChanged += ApplyLocale;

        LoadData();
        SetupBindings();
        ApplyTheme();
        ApplyLocale();
        SetupTrayIcon();

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _uiTimer.Tick += UpdateTimerDisplay;
        _uiTimer.Start();

        UpdateStatsDisplay();
    }

    private void SetupTrayIcon()
    {
        var iconPath = PathIO.Combine(AppDomain.CurrentDomain.BaseDirectory, "gemini-svg.ico");
        _trayIcon = new TrayIconService(this, iconPath,
            LocaleManager.Current == Locale.ZH ? "显示窗口" : "Show",
            LocaleManager.Current == Locale.ZH ? "停止专注" : "Stop Focus",
            LocaleManager.Current == Locale.ZH ? "退出" : "Exit");

        _trayIcon.ShowRequested += ShowWindow;
        _trayIcon.StopRequested += () => Dispatcher.Invoke(() => StopBtn_Click(this, new RoutedEventArgs()));
        _trayIcon.ExitRequested += () =>
        {
            if (_session is { IsActive: true })
            {
                Dispatcher.Invoke(() => StopBtn_Click(this, new RoutedEventArgs()));
            }
            _trayIcon!.Dispose();
            _trayIcon = null;
            System.Windows.Application.Current.Shutdown();
        };
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        ShowInTaskbar = true;
        Activate();
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        if (_trayIcon != null)
            _trayIcon.Visible = true;
    }

    private void FlashAchievement(string names)
    {
        StatusBarText.Text = $"🎉 {names} — {(LocaleManager.Current == Locale.ZH ? "成就解锁！" : "Achievement unlocked!")}";
        StatusBarText.Foreground = new SolidColorBrush(Color.FromRgb(99, 102, 241));

        // Revert after 5 seconds
        _ = Task.Run(async () =>
        {
            await Task.Delay(5000);
            Dispatcher.Invoke(() =>
            {
                if (_session == null)
                {
                    StatusBarText.Text = L("Status.NotMonitoring");
                    StatusBarText.Foreground = new SolidColorBrush(GrayColor());
                }
            });
        });
    }

    private void LoadData()
    {
        _loading = true;

        _appWhitelist = WhitelistStore.LoadAppWhitelist();
        _urlWhitelist = WhitelistStore.LoadUrlWhitelist();
        _todoList = WhitelistStore.LoadTodoList();
        _archivedTasks = WhitelistStore.LoadArchive();
        _abilities = WhitelistStore.LoadAbilities();
        _settings = WhitelistStore.LoadSettings();

        DurationSlider.Value = _settings.FocusDurationMinutes;
        DurationText.Text = $"{_settings.FocusDurationMinutes} {L("Settings.DurationUnit")}";
        EnforceUrlCheck.IsChecked = _settings.EnforceUrlWhitelist;
        AutoStartCheck.IsChecked = _settings.AutoStart;

        // Wire events AFTER setting saved values, so init doesn't trigger saves
        DurationSlider.ValueChanged += DurationSlider_ValueChanged;
        EnforceUrlCheck.Checked += EnforceUrlCheck_Changed;
        EnforceUrlCheck.Unchecked += EnforceUrlCheck_Changed;
        AutoStartCheck.Checked += AutoStartCheck_Changed;
        AutoStartCheck.Unchecked += AutoStartCheck_Changed;

        _loading = false;
    }

    private void SetupBindings()
    {
        AppWhitelistList.ItemsSource = _appWhitelist;
        UrlWhitelistList.ItemsSource = _urlWhitelist;
        TodoList.ItemsSource = _todoList;
        ArchiveList.ItemsSource = _archivedTasks;
        AbilityList.ItemsSource = _abilities;
        DrawRadarChart();
    }

    private void UpdateTimerDisplay(object? sender, EventArgs e)
    {
        if (_session == null) return;

        if (_session.IsActive)
        {
            var elapsed = _session.Elapsed;
            var remaining = _session.TargetDuration - elapsed;

            TimerText.Text = elapsed.ToString(@"hh\:mm\:ss");

            var textPrimary = _settings.IsDarkMode
                ? new SolidColorBrush(Color.FromRgb(249, 250, 251))
                : new SolidColorBrush(Color.FromRgb(31, 41, 55));

            if (remaining.TotalSeconds > 0)
            {
                TimerSubtext.Text = F("Timer.Remaining", remaining.ToString(@"hh\:mm\:ss"));
                TimerText.Foreground = textPrimary;
            }
            else
            {
                TimerSubtext.Text = L("Timer.Complete");
                TimerText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }

            StatusLabel.Text = L("Header.Active");
            StatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            StatusBarText.Text = F("Status.Active", _session.ViolationCount);

            if (_session.ViolationCount > 0)
                ViolationCountLabel.Text = F("Status.Violations", _session.ViolationCount);

            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;
            DurationSlider.IsEnabled = false;

            UpdateCurrentTaskLabel();
        }
    }

    private void UpdateCurrentTaskLabel()
    {
        if (_session is { IsActive: true })
        {
            var task = _todoList.FirstOrDefault(t => !t.IsCompleted && !t.IsFinished);
            if (task != null)
            {
                var zh = LocaleManager.Current == Locale.ZH;
                var icon = task.Type switch
                {
                    TodoType.Daily => "🔄",
                    TodoType.LongTerm => "🎯",
                    _ => "📋"
                };
                CurrentTaskLabel.Text = $"{icon} {(zh ? "当前：" : "Current: ")}{task.Text}";
            }
            else
            {
                CurrentTaskLabel.Text = "";
            }
        }
        else
        {
            CurrentTaskLabel.Text = "";
        }
    }

    private string L(string key) => LocaleManager.Get(key);

    private string F(string key, params object[] args) => string.Format(LocaleManager.Get(key), args);

    private void ApplyLocale()
    {
        // Window
        Title = L("App.Title");

        // Header
        StatusLabel.Text = L("Header.Ready");
        StatusLabel.Foreground = new SolidColorBrush(GrayColor());

        // Buttons
        StartBtn.Content = L("Btn.Start");
        StopBtn.Content = L("Btn.Stop");
        AddAppBtn.Content = L("Btn.AddApp");
        RemoveAppBtn.Content = L("Btn.Remove");

        // Section titles
        AppWhitelistTitle.Text = L("Whitelist.Apps");
        UrlWhitelistTitle.Text = L("Whitelist.Urls");
        TodoTitle.Text = L("Todo.Title");
        RadarTitle.Text = L("Radar.Title");
        AbilityEditorTitle.Text = L("Abilities.Title");
        SettingsTitle.Text = "Settings"; // not localized, neutral for now

        // Settings
        DurationLabel.Text = L("Settings.Duration");
        EnforceUrlCheck.Content = L("Settings.EnforceUrl");
        AutoStartCheck.Content = L("Settings.AutoStart");
        DurationText.Text = $"{_settings.FocusDurationMinutes} {L("Settings.DurationUnit")}";

        // Language button
        LangBtn.Content = LocaleManager.Current == Locale.ZH ? "中/EN" : "EN/中";

        // Dark mode button tooltip
        DarkModeBtn.ToolTip = LocaleManager.Current == Locale.ZH ? "切换深色模式" : "Toggle dark mode";

        // Archive button
        UpdateArchiveButton();

        // Task type buttons
        UpdateTaskTypeButtons();

        // Current task label refresh
        UpdateCurrentTaskLabel();

        // Tray menu
        _trayIcon?.UpdateMenuTexts(
            LocaleManager.Current == Locale.ZH ? "显示窗口" : "Show",
            LocaleManager.Current == Locale.ZH ? "停止专注" : "Stop Focus",
            LocaleManager.Current == Locale.ZH ? "退出" : "Exit");

        UpdateStatsDisplay();

        // Re-apply status bar if not in session
        if (_session == null || !_session.IsActive)
        {
            StatusBarText.Text = L("Status.NotMonitoring");
            StatusBarText.Foreground = new SolidColorBrush(GrayColor());
            StatusDot.Fill = new SolidColorBrush(LightGrayColor());
        }
    }

    private void LangBtn_Click(object sender, RoutedEventArgs e)
    {
        LocaleManager.Toggle();
        WhitelistStore.SaveLocale(LocaleManager.Current);
    }

    private void UpdateStatsDisplay()
    {
        var s = _settings;
        var zh = LocaleManager.Current == Locale.ZH;

        StatsText.Text = F("Header.Stats", s.TotalSessionsCompleted,
            s.TotalFocusTime.Hours, s.TotalFocusTime.Minutes);

        var level = s.Level;
        LevelText.Text = F("Header.Level", level, AchievementService.LevelName(level, zh));

        if (level < 7)
        {
            var next = s.NextLevelThreshold;
            var cur = s.TotalFocusTime;
            LevelProgressBar.Visibility = Visibility.Visible;
            LevelProgressBar.Maximum = next.TotalHours;
            LevelProgressBar.Value = cur.TotalHours;
            LevelProgressText.Text = $"{cur.TotalHours:F1} / {next.TotalHours:F0} h";
        }
        else
        {
            LevelProgressBar.Visibility = Visibility.Collapsed;
            LevelProgressText.Text = zh ? "已达最高等级！" : "Max level reached!";
        }

        // Streak
        if (s.CurrentStreak > 0)
            StreakText.Text = $"🔥 {s.CurrentStreak}";
        else
            StreakText.Text = "";

        // Achievement badges (show unlocked count)
        var total = AchievementService.All.Length;
        var unlocked = s.UnlockedAchievements.Count;
        AchievementsText.Text = unlocked > 0 ? $"🏅 {unlocked}/{total}" : "";

        // Tooltip with unlocked achievement names
        if (unlocked > 0)
        {
            var names = AchievementService.All
                .Where(a => s.UnlockedAchievements.Contains(a.Id))
                .Select(a => zh ? a.NameZh : a.NameEn);
            AchievementsText.ToolTip = string.Join("\n", names);
        }
    }

    private void StartBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_session != null)
        {
            _session.AppViolation -= OnAppViolation;
            _session.UrlViolation -= OnUrlViolation;
            _session.Dispose();
        }

        _session = new FocusSessionManager(_appWhitelist, _urlWhitelist, _settings);
        _session.AppViolation += OnAppViolation;
        _session.UrlViolation += OnUrlViolation;
        _session.StateChanged += () => Dispatcher.Invoke(UpdateStatsDisplay);
        _session.Start();

        UpdateTimerDisplay(null, EventArgs.Empty);

        StartBtn.IsEnabled = false;
        StopBtn.IsEnabled = true;
        DurationSlider.IsEnabled = false;

        HideToTray();
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        List<Achievement>? newAchievements = null;

        if (_session != null)
        {
            _session.Stop();
            newAchievements = _session.NewAchievements;
            _session.AppViolation -= OnAppViolation;
            _session.UrlViolation -= OnUrlViolation;
            _session.Dispose();
            _session = null;
        }

        TimerText.Text = "00:00:00";
        TimerText.Foreground = _settings.IsDarkMode
            ? new SolidColorBrush(Color.FromRgb(249, 250, 251))
            : new SolidColorBrush(Color.FromRgb(31, 41, 55));
        TimerSubtext.Text = "";
        CurrentTaskLabel.Text = "";
        StatusLabel.Text = L("Header.Ready");
        StatusLabel.Foreground = new SolidColorBrush(GrayColor());
        StatusDot.Fill = new SolidColorBrush(LightGrayColor());
        StatusBarText.Text = L("Status.NotMonitoring");
        ViolationCountLabel.Text = "";

        StartBtn.IsEnabled = true;
        StopBtn.IsEnabled = false;
        DurationSlider.IsEnabled = true;

        UpdateStatsDisplay();

        ShowWindow();
        if (_trayIcon != null) _trayIcon.Visible = false;

        // Flash new achievements if any
        if (newAchievements is { Count: > 0 })
        {
            var zh = LocaleManager.Current == Locale.ZH;
            var names = newAchievements.Select(a => zh ? a.NameZh : a.NameEn);
            FlashAchievement(string.Join(", ", names));
        }
    }

    private void DurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        var minutes = (int)DurationSlider.Value;
        DurationText.Text = $"{minutes} {L("Settings.DurationUnit")}";
        _settings.FocusDurationMinutes = minutes;
        WhitelistStore.SaveSettings(_settings);
    }

    private void OnAppViolation(Process proc, string title)
    {
        // Force-close: immediately kill the non-whitelisted app, no prompt
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnAppViolation(proc, title));
            return;
        }

        StatusBarText.Text = F("Status.BlockedApp", proc.ProcessName);
        ForceCloseApplication(proc);
        ViolationCountLabel.Text = F("Status.Violations", _session?.ViolationCount ?? 0);
        UpdateTimerDisplay(null, EventArgs.Empty);
    }

    private void OnUrlViolation(Process proc, string url, string title)
    {
        // Force-close tab for non-whitelisted URL, no prompt
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnUrlViolation(proc, url, title));
            return;
        }

        StatusBarText.Text = F("Status.BlockedUrl", url);
        CloseBrowserTab(proc);
        ViolationCountLabel.Text = $"{_session?.ViolationCount ?? 0} violations";
        UpdateTimerDisplay(null, EventArgs.Empty);
    }

    private void ForceCloseApplication(Process proc)
    {
        try
        {
            if (!proc.HasExited)
            {
                // Try graceful close first
                if (proc.CloseMainWindow())
                {
                    proc.WaitForExit(2000);
                }
                // Force kill
                if (!proc.HasExited)
                {
                    proc.Kill();
                    proc.WaitForExit(3000);
                }
            }
        }
        catch
        {
            // Process may have already exited or we don't have permission
        }
    }

    private static void CloseBrowserTab(Process proc)
    {
        try
        {
            if (proc.HasExited) return;

            // Send Ctrl+W to close the current tab
            var hWnd = proc.MainWindowHandle;
            if (hWnd != IntPtr.Zero)
            {
                NativeMethods.SetForegroundWindow(hWnd);
                System.Threading.Thread.Sleep(100);

                // Simulate Ctrl+W
                const int KEYEVENTF_KEYUP = 0x0002;
                const int VK_CONTROL = 0x11;
                const int VK_W = 0x57;

                // Key down
                NativeMethods.keybd_event(VK_CONTROL, 0, 0, IntPtr.Zero);
                NativeMethods.keybd_event(VK_W, 0, 0, IntPtr.Zero);

                System.Threading.Thread.Sleep(50);

                // Key up
                NativeMethods.keybd_event(VK_W, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
                NativeMethods.keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
            }
        }
        catch { }
    }

    private static bool IsKnownBrowser(string processName)
    {
        var browsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "chrome", "msedge", "firefox", "opera", "brave", "iexplore", "chromium"
        };
        return browsers.Contains(processName);
    }

    private void AddAppBtn_Click(object sender, RoutedEventArgs e)
    {
        var input = ShowInputDialog(
            L("Dialog.AddApp.Title"),
            L("Dialog.AddApp.Message"));

        if (string.IsNullOrWhiteSpace(input)) return;

        var processName = input.Trim();
        if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            processName = processName[..^4];

        if (_appWhitelist.Any(a => a.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(F("Dialog.Duplicate", processName),
                L("Dialog.AlreadyExists"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var entry = new AppWhitelistEntry
        {
            ProcessName = processName,
            DisplayName = processName,
            IsBrowser = IsKnownBrowser(processName)
        };

        _appWhitelist.Add(entry);
        WhitelistStore.SaveAppWhitelist(_appWhitelist);
        AppWhitelistList.Items.Refresh();
    }

    private void RemoveAppBtn_Click(object sender, RoutedEventArgs e)
    {
        var enabled = _appWhitelist.Where(a => a.IsEnabled).ToList();
        if (enabled.Count == 0)
        {
            MessageBox.Show(L("Dialog.NoApps"), L("Dialog.Info"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new AppRemoveDialog(enabled, L("Dialog.Remove.Title"), L("Dialog.Cancel"), L("Dialog.RemoveBtn"));
        dialog.Owner = this;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        if (dialog.ShowDialog() == true && dialog.SelectedApp != null)
        {
            _appWhitelist.Remove(dialog.SelectedApp);
            WhitelistStore.SaveAppWhitelist(_appWhitelist);
            AppWhitelistList.Items.Refresh();
        }
    }

    private void AddUrlBtn_Click(object sender, RoutedEventArgs e)
    {
        var input = NewUrlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            MessageBox.Show(L("Dialog.UrlRequired"),
                L("Dialog.InputRequired"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var pattern = CleanUrlPattern(input);
        if (string.IsNullOrEmpty(pattern))
        {
            MessageBox.Show(L("Dialog.InvalidUrl"),
                L("Dialog.InvalidURL"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_urlWhitelist.Any(u => u.Pattern.Equals(pattern, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(F("Dialog.Duplicate", pattern),
                L("Dialog.AlreadyExists"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _urlWhitelist.Add(new UrlWhitelistEntry { Pattern = pattern, Description = pattern });
        WhitelistStore.SaveUrlWhitelist(_urlWhitelist);
        UrlWhitelistList.Items.Refresh();
        NewUrlBox.Text = "";
    }

    private static string CleanUrlPattern(string input)
    {
        // Strip protocol
        var cleaned = input.Trim();
        var idx = cleaned.IndexOf("://");
        if (idx >= 0) cleaned = cleaned[(idx + 3)..];

        // Strip www. prefix
        if (cleaned.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[4..];

        // Strip trailing slash
        cleaned = cleaned.TrimEnd('/');

        return cleaned;
    }

    private void AddTaskBtn_Click(object sender, RoutedEventArgs e)
    {
        AddTask();
    }

    private void NewTaskBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
            AddTask();
    }

    private void AddTask()
    {
        var text = NewTaskBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var boosts = _boostItems
            .Where(b => b.BoostPoints > 0)
            .Select(b => new AbilityBoost { AbilityName = b.Name, Points = b.BoostPoints })
            .ToList();

        _todoList.Add(new TodoItem
        {
            Text = text,
            Type = _taskType,
            LastResetDate = today,
            Boosts = boosts
        });
        WhitelistStore.SaveTodoList(_todoList);
        TodoList.Items.Refresh();

        // Reset boost selections
        _boostItems.Clear();
        BoostSelector.Visibility = Visibility.Collapsed;
        UpdateTaskTypeButtons();

        NewTaskBox.Text = "";
        UpdateCurrentTaskLabel();
    }

    private void TaskTypeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            _taskType = tag switch
            {
                "Daily" => TodoType.Daily,
                "LongTerm" => TodoType.LongTerm,
                _ => TodoType.ShortTerm
            };
            UpdateTaskTypeButtons();
        }
    }

    private void BoostToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        var visible = BoostSelector.Visibility == Visibility.Visible;
        BoostSelector.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        if (!visible)
        {
            _boostItems.Clear();
            foreach (var ab in _abilities)
                _boostItems.Add(new BoostSelectorItem { Name = ab.Name, Icon = ab.Icon });
            BoostSelector.ItemsSource = _boostItems;
        }
    }

    private void BoostPlusBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is BoostSelectorItem item)
        {
            item.BoostPoints++;
            BoostSelector.ItemsSource = null;
            BoostSelector.ItemsSource = _boostItems;
        }
    }

    private void BoostMinusBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is BoostSelectorItem item)
        {
            if (item.BoostPoints > 0) item.BoostPoints--;
            BoostSelector.ItemsSource = null;
            BoostSelector.ItemsSource = _boostItems;
        }
    }

    private void EditAbilityBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Ability ab)
        {
            var zh = LocaleManager.Current == Locale.ZH;
            var input = ShowInputDialog(
                zh ? "重命名属性" : "Rename Ability",
                zh ? $"输入 {ab.Name} 的新名称：" : $"Enter new name for {ab.Name}:");
            if (!string.IsNullOrWhiteSpace(input))
            {
                var newName = input.Trim();
                // Update boosts in all tasks referencing old name
                foreach (var task in _todoList)
                {
                    foreach (var boost in task.Boosts)
                    {
                        if (boost.AbilityName == ab.Name)
                            boost.AbilityName = newName;
                    }
                }
                foreach (var task in _archivedTasks)
                {
                    foreach (var boost in task.Boosts)
                    {
                        if (boost.AbilityName == ab.Name)
                            boost.AbilityName = newName;
                    }
                }
                ab.Name = newName;
                WhitelistStore.SaveAbilities(_abilities);
                WhitelistStore.SaveTodoList(_todoList);
                WhitelistStore.SaveArchive(_archivedTasks);
                AbilityList.Items.Refresh();
                TodoList.Items.Refresh();
                DrawRadarChart();
            }
        }
    }

    private void UpdateTaskTypeButtons()
    {
        var zh = LocaleManager.Current == Locale.ZH;
        var activeBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241));
        var inactiveBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235));
        var whiteBrush = new SolidColorBrush(Colors.White);
        var textBrush = new SolidColorBrush(Color.FromRgb(107, 114, 128));

        void StyleBtn(Button btn, bool active)
        {
            btn.Background = active ? activeBrush : inactiveBrush;
            btn.Foreground = active ? whiteBrush : textBrush;
            btn.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
        }

        StyleBtn(ShortTermBtn, _taskType == TodoType.ShortTerm);
        StyleBtn(DailyBtn, _taskType == TodoType.Daily);
        StyleBtn(LongTermBtn, _taskType == TodoType.LongTerm);

        ShortTermBtn.Content = zh ? "短期" : "Short";
        DailyBtn.Content = (zh ? "🔄 每日" : "🔄 Daily");
        LongTermBtn.Content = (zh ? "🎯 长期" : "🎯 Long");
    }

    private void FinishTaskBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TodoItem item && item.Type == TodoType.LongTerm)
        {
            _todoList.Remove(item);
            _archivedTasks.Insert(0, item);
            WhitelistStore.SaveTodoList(_todoList);
            WhitelistStore.SaveArchive(_archivedTasks);
            TodoList.Items.Refresh();
            ArchiveList.Items.Refresh();
            UpdateArchiveButton();
            UpdateCurrentTaskLabel();
        }
    }

    private void TaskCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.DataContext is not TodoItem task) return;
        if (!task.IsCompleted) return;

        // Auto-apply preset boosts
        foreach (var boost in task.Boosts)
        {
            var ab = _abilities.FirstOrDefault(a => a.Name == boost.AbilityName);
            if (ab != null) ab.Points += boost.Points;
        }
        if (task.Boosts.Count > 0)
        {
            WhitelistStore.SaveAbilities(_abilities);
            AbilityList.Items.Refresh();
            DrawRadarChart();
        }

        // Archive completed short-term tasks
        if (task.Type == TodoType.ShortTerm)
        {
            _todoList.Remove(task);
            _archivedTasks.Insert(0, task);
        }

        WhitelistStore.SaveTodoList(_todoList);
        WhitelistStore.SaveArchive(_archivedTasks);
        TodoList.Items.Refresh();
        ArchiveList.Items.Refresh();
        UpdateArchiveButton();
        UpdateCurrentTaskLabel();
    }

    private void DeleteTaskBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TodoItem item)
        {
            _todoList.Remove(item);
            WhitelistStore.SaveTodoList(_todoList);
            TodoList.Items.Refresh();
            UpdateCurrentTaskLabel();
        }
    }

    private void DrawRadarChart()
    {
        var canvas = RadarCanvas;
        canvas.Children.Clear();
        int n = _abilities.Count;
        if (n < 3) return;

        double cx = 100, cy = 100, r = 90;
        var dark = _settings.IsDarkMode;
        var gridStroke = ParseColor(dark ? "#4B5563" : "#D1D5DB");
        var axisStroke = ParseColor(dark ? "#6B7280" : "#9CA3AF");
        var fillBrush = new SolidColorBrush(ParseColor("#6366F1")) { Opacity = 0.25 };
        var strokeBrush = new SolidColorBrush(ParseColor("#6366F1"));
        var labelFg = dark ? Color.FromRgb(209, 213, 219) : Color.FromRgb(55, 65, 81);

        // Concentric grid
        for (int level = 1; level <= 4; level++)
        {
            var poly = new Polygon
            {
                Stroke = new SolidColorBrush(gridStroke),
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 3, 2 }
            };
            double lr = r * level / 4;
            for (int i = 0; i < n; i++)
            {
                double angle = -Math.PI / 2 + 2 * Math.PI * i / n;
                poly.Points.Add(new Point(cx + lr * Math.Cos(angle), cy + lr * Math.Sin(angle)));
            }
            canvas.Children.Add(poly);
        }

        // Axis lines
        for (int i = 0; i < n; i++)
        {
            double angle = -Math.PI / 2 + 2 * Math.PI * i / n;
            double ex = cx + r * Math.Cos(angle), ey = cy + r * Math.Sin(angle);
            var line = new System.Windows.Shapes.Line
            {
                X1 = cx, Y1 = cy, X2 = ex, Y2 = ey,
                Stroke = new SolidColorBrush(axisStroke),
                StrokeThickness = 0.8
            };
            canvas.Children.Add(line);

            // Label
            var ab = _abilities[i];
            double lx = cx + (r + 18) * Math.Cos(angle) - 20;
            double ly = cy + (r + 18) * Math.Sin(angle) - 8;
            var lbl = new TextBlock
            {
                Text = $"{ab.Icon} {ab.Name}\n{ab.Points}",
                FontSize = 9,
                Foreground = new SolidColorBrush(labelFg),
                TextAlignment = TextAlignment.Center,
                Width = 45,
                Height = 24
            };
            Canvas.SetLeft(lbl, lx);
            Canvas.SetTop(lbl, ly);
            canvas.Children.Add(lbl);
        }

        // Data polygon
        int maxPts = _abilities.Max(a => a.Points);
        if (maxPts == 0) maxPts = 1;
        // Scale up so max is visually meaningful (target: points fill the chart proportionally)
        double scaleTarget = Math.Max(maxPts, 10); // at least 10 as "full" radius
        var dataPoly = new Polygon
        {
            Fill = fillBrush,
            Stroke = strokeBrush,
            StrokeThickness = 2
        };
        for (int i = 0; i < n; i++)
        {
            double frac = Math.Min(_abilities[i].Points / scaleTarget, 1.0);
            double angle = -Math.PI / 2 + 2 * Math.PI * i / n;
            dataPoly.Points.Add(new Point(cx + r * frac * Math.Cos(angle), cy + r * frac * Math.Sin(angle)));
        }
        canvas.Children.Add(dataPoly);

        // Data points (dots)
        for (int i = 0; i < n; i++)
        {
            double frac = Math.Min(_abilities[i].Points / scaleTarget, 1.0);
            double angle = -Math.PI / 2 + 2 * Math.PI * i / n;
            double px = cx + r * frac * Math.Cos(angle), py = cy + r * frac * Math.Sin(angle);
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 6, Height = 6,
                Fill = strokeBrush,
                Stroke = Brushes.White,
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(dot, px - 3);
            Canvas.SetTop(dot, py - 3);
            canvas.Children.Add(dot);
        }
    }

    private void NewAbilityBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) AddAbility();
    }

    private void AddAbilityBtn_Click(object sender, RoutedEventArgs e) => AddAbility();

    private void AddAbility()
    {
        var text = NewAbilityBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        var colors = new[] { "#6366F1", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#EC4899", "#06B6D4" };
        var color = colors[_abilities.Count % colors.Length];

        _abilities.Add(new Ability { Name = text, Icon = "⭐", Color = color });
        WhitelistStore.SaveAbilities(_abilities);
        AbilityList.Items.Refresh();
        DrawRadarChart();
        NewAbilityBox.Text = "";
    }

    private void AbilityAddPointBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Ability ab)
        {
            ab.Points++;
            WhitelistStore.SaveAbilities(_abilities);
            AbilityList.Items.Refresh();
            DrawRadarChart();
        }
    }

    private void DeleteAbilityBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Ability ab)
        {
            _abilities.Remove(ab);
            WhitelistStore.SaveAbilities(_abilities);
            AbilityList.Items.Refresh();
            DrawRadarChart();
        }
    }

    private void ArchiveToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        var visible = ArchiveList.Visibility == Visibility.Visible;
        ArchiveList.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        UpdateArchiveButton();
    }

    private void UpdateArchiveButton()
    {
        var zh = LocaleManager.Current == Locale.ZH;
        var n = _archivedTasks.Count;
        ArchiveToggleBtn.Content = zh
            ? $"📦 归档 ({n})"
            : $"📦 Archive ({n})";
    }

    private void DarkModeBtn_Click(object sender, RoutedEventArgs e)
    {
        _settings.IsDarkMode = !_settings.IsDarkMode;
        WhitelistStore.SaveSettings(_settings);
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        var dark = _settings.IsDarkMode;
        DarkModeBtn.Content = dark ? "☀️" : "🌙";

        Resources["WindowBg"] = new SolidColorBrush(ParseColor(dark ? "#111827" : "#F9FAFB"));
        Resources["BgCard"] = new SolidColorBrush(ParseColor(dark ? "#1F2937" : "#FFFFFF"));
        Resources["ItemBg"] = new SolidColorBrush(ParseColor(dark ? "#374151" : "#F9FAFB"));
        Resources["BtnSecondaryBg"] = new SolidColorBrush(ParseColor(dark ? "#374151" : "#FFFFFF"));
        Resources["BtnSecondaryFg"] = new SolidColorBrush(ParseColor(dark ? "#F9FAFB" : "#1F2937"));
        Resources["TextPrimary"] = new SolidColorBrush(ParseColor(dark ? "#F9FAFB" : "#1F2937"));
        Resources["TextSecondary"] = new SolidColorBrush(ParseColor(dark ? "#9CA3AF" : "#6B7280"));
        Resources["BorderColor"] = new SolidColorBrush(ParseColor(dark ? "#374151" : "#E5E7EB"));

        DrawRadarChart();
    }

    private static Color ParseColor(string hex) =>
        (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);

    private Color GrayColor() => _settings.IsDarkMode
        ? Color.FromRgb(156, 163, 175) : Color.FromRgb(107, 114, 128);

    private Color LightGrayColor() => _settings.IsDarkMode
        ? Color.FromRgb(107, 114, 128) : Color.FromRgb(156, 163, 175);

    private void RemoveUrlBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is UrlWhitelistEntry entry)
        {
            _urlWhitelist.Remove(entry);
            WhitelistStore.SaveUrlWhitelist(_urlWhitelist);
            UrlWhitelistList.Items.Refresh();
        }
    }

    private void OpenUrlInChrome_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is UrlWhitelistEntry entry)
        {
            var url = entry.Pattern;
            if (!url.Contains("://"))
                url = "https://" + url;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "chrome.exe",
                    Arguments = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Fallback: open with default browser
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }
    }

    private void EnforceUrlCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var enforce = EnforceUrlCheck.IsChecked == true;
        _settings.EnforceUrlWhitelist = enforce;
        WhitelistStore.SaveSettings(_settings);
        _session?.SetEnforceUrls(enforce);
    }

    private void AutoStartCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var autoStart = AutoStartCheck.IsChecked == true;
        _settings.AutoStart = autoStart;
        WhitelistStore.SaveSettings(_settings);

        var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
        var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var startupLink = PathIO.Combine(startupFolder, "Focus Mode.lnk");

        if (autoStart)
        {
            CreateShortcut(exePath, startupLink);
        }
        else
        {
            try { File.Delete(startupLink); } catch { }
        }
    }

    private static void CreateShortcut(string exePath, string linkPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            var shell = Activator.CreateInstance(shellType);
            if (shell == null) return;
            dynamic shortcut = shell.GetType().InvokeMember("CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { linkPath })!;
            shortcut.TargetPath = exePath;
            shortcut.WorkingDirectory = PathIO.GetDirectoryName(exePath);
            shortcut.Description = "Focus Mode";
            shortcut.IconLocation = PathIO.Combine(PathIO.GetDirectoryName(exePath) ?? "", "gemini-svg.ico") + ",0";
            shortcut.Save();
        }
        catch { }
    }

    private string? ShowInputDialog(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false
        };

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var msgBlock = new TextBlock
        {
            Text = message,
            Margin = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13
        };
        Grid.SetRow(msgBlock, 0);
        grid.Children.Add(msgBlock);

        var textBox = new TextBox
        {
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 14
        };
        Grid.SetRow(textBox, 1);
        grid.Children.Add(textBox);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetRow(btnPanel, 3);

        var cancelBtn = new Button
        {
            Content = L("Dialog.Cancel"),
            Padding = new Thickness(16, 6, 16, 6),
            Margin = new Thickness(0, 0, 8, 0)
        };
        cancelBtn.Click += (_, _) => { dialog.DialogResult = false; dialog.Close(); };
        btnPanel.Children.Add(cancelBtn);

        var okBtn = new Button
        {
            Content = L("Dialog.OK"),
            Padding = new Thickness(16, 6, 16, 6)
        };
        okBtn.Click += (_, _) => { dialog.DialogResult = true; dialog.Close(); };
        btnPanel.Children.Add(okBtn);

        grid.Children.Add(btnPanel);
        dialog.Content = grid;

        textBox.Focus();

        return dialog.ShowDialog() == true ? textBox.Text : null;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_session is { IsActive: true })
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        _session?.Stop();
        _session?.Dispose();
        _uiTimer?.Stop();
        _trayIcon?.Dispose();
        _trayIcon = null;
        base.OnClosing(e);
    }
}
