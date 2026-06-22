using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using WpfApp1.Models;

namespace WpfApp1.Services;

/// <summary>
/// Manages the focus session lifecycle and coordinates monitoring.
/// </summary>
public class FocusSessionManager : IDisposable
{
    private readonly ProcessMonitorService _monitor;
    private readonly ObservableCollection<AppWhitelistEntry> _appWhitelist;
    private readonly ObservableCollection<UrlWhitelistEntry> _urlWhitelist;
    private readonly System.Windows.Threading.DispatcherTimer _timer;
    private readonly FocusSettings _settings;

    public event Action? StateChanged;
    public event Action<Process, string>? AppViolation;
    public event Action<Process, string, string>? UrlViolation;

    public bool IsActive { get; private set; }
    public DateTime? StartTime { get; private set; }
    public TimeSpan Elapsed =>
        IsActive && StartTime.HasValue ? DateTime.Now - StartTime.Value : TimeSpan.Zero;
    public TimeSpan TargetDuration { get; set; } = TimeSpan.FromMinutes(25);
    public int ViolationCount { get; private set; }
    public int MonitorIntervalMs => _settings.MonitorIntervalMs;

    // Stats — read from settings on start, written back on stop
    public int TotalSessionsCompleted => _settings.TotalSessionsCompleted;
    public TimeSpan TotalFocusTime => _settings.TotalFocusTime;

    /// <summary>Newly unlocked achievements since this session started.</summary>
    public List<Achievement> NewAchievements { get; private set; } = new();

    public FocusSessionManager(
        ObservableCollection<AppWhitelistEntry> appWhitelist,
        ObservableCollection<UrlWhitelistEntry> urlWhitelist,
        FocusSettings settings)
    {
        _appWhitelist = appWhitelist;
        _urlWhitelist = urlWhitelist;
        _settings = settings;
        TargetDuration = TimeSpan.FromMinutes(settings.FocusDurationMinutes);

        _monitor = new ProcessMonitorService(appWhitelist, urlWhitelist, settings.EnforceUrlWhitelist);
        _monitor.ViolationDetected += OnAppViolation;
        _monitor.UrlViolationDetected += OnUrlViolation;

        // UI update timer
        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timer.Tick += (_, _) => StateChanged?.Invoke();
    }

    public void Start()
    {
        if (IsActive) return;

        IsActive = true;
        StartTime = DateTime.Now;
        ViolationCount = 0;
        _monitor.EnforceUrls = _settings.EnforceUrlWhitelist;
        _monitor.Start(_settings.MonitorIntervalMs);
        _timer.Start();
        StateChanged?.Invoke();
    }

    public void Stop()
    {
        if (!IsActive) return;

        if (StartTime.HasValue)
        {
            var elapsed = DateTime.Now - StartTime.Value;
            // Count as completed if at least 1 minute elapsed
            if (elapsed >= TimeSpan.FromMinutes(1))
            {
                _settings.TotalSessionsCompleted++;
                _settings.TotalFocusTime += elapsed;

                // Update streak
                var today = DateTime.Today.ToString("yyyy-MM-dd");
                var yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
                if (_settings.LastSessionDate == yesterday)
                    _settings.CurrentStreak++;
                else if (_settings.LastSessionDate != today)
                    _settings.CurrentStreak = 1;
                _settings.LastSessionDate = today;
                if (_settings.CurrentStreak > _settings.BestStreak)
                    _settings.BestStreak = _settings.CurrentStreak;

                // Check achievements
                NewAchievements = AchievementService.CheckAndUnlock(_settings);
            }
            else
            {
                NewAchievements = new();
            }
        }

        WhitelistStore.SaveSettings(_settings);

        IsActive = false;
        StartTime = null;
        _monitor.Stop();
        _timer.Stop();
        StateChanged?.Invoke();
    }

    public void SetTargetMinutes(int minutes)
    {
        TargetDuration = TimeSpan.FromMinutes(minutes);
        _settings.FocusDurationMinutes = minutes;
        WhitelistStore.SaveSettings(_settings);
    }

    public void SetEnforceUrls(bool enforce)
    {
        _settings.EnforceUrlWhitelist = enforce;
        _monitor.EnforceUrls = enforce;
        WhitelistStore.SaveSettings(_settings);
    }

    private void OnAppViolation(Process proc, string title)
    {
        ViolationCount++;
        Application.Current.Dispatcher.Invoke(() =>
        {
            AppViolation?.Invoke(proc, title);
        });
    }

    private void OnUrlViolation(Process proc, string url, string title)
    {
        ViolationCount++;
        Application.Current.Dispatcher.Invoke(() =>
        {
            UrlViolation?.Invoke(proc, url, title);
        });
    }

    public void Dispose()
    {
        Stop();
        _monitor.Dispose();
        GC.SuppressFinalize(this);
    }
}
