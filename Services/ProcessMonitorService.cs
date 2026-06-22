using System.Collections.ObjectModel;
using System.Diagnostics;
using WpfApp1.Models;

namespace WpfApp1.Services;

/// <summary>
/// Monitors running processes and detects non-whitelisted foreground apps.
/// Also monitors browser URLs when a browser is the active window.
/// </summary>
public class ProcessMonitorService : IDisposable
{
    private readonly ObservableCollection<AppWhitelistEntry> _appWhitelist;
    private readonly ObservableCollection<UrlWhitelistEntry> _urlWhitelist;
    private readonly BrowserUrlDetector _urlDetector = new();
    private bool _enforceUrls;
    private bool _isRunning;
    private CancellationTokenSource? _cts;

    public event Action<Process, string>? ViolationDetected;
    public event Action<Process, string, string>? UrlViolationDetected;

    public ProcessMonitorService(
        ObservableCollection<AppWhitelistEntry> appWhitelist,
        ObservableCollection<UrlWhitelistEntry> urlWhitelist,
        bool enforceUrls = true)
    {
        _appWhitelist = appWhitelist;
        _urlWhitelist = urlWhitelist;
        _enforceUrls = enforceUrls;
    }

    public bool EnforceUrls
    {
        get => _enforceUrls;
        set => _enforceUrls = value;
    }

    public void Start(int intervalMs = 1500)
    {
        if (_isRunning) return;
        _isRunning = true;
        _cts = new CancellationTokenSource();
        _ = MonitorLoop(intervalMs, _cts.Token);
    }

    public void Stop()
    {
        _isRunning = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task MonitorLoop(int intervalMs, CancellationToken ct)
    {
        var lastViolationProcess = string.Empty;
        var lastViolationTime = DateTime.MinValue;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(intervalMs, ct);

                var hWnd = NativeMethods.GetForegroundWindow();
                if (hWnd == IntPtr.Zero) continue;

                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == 0) continue;

                try
                {
                    var proc = Process.GetProcessById((int)pid);
                    var procName = proc.ProcessName;

                    // Skip self
                    if (procName.Equals("WpfApp1", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Skip system/background processes
                    if (IsSystemProcess(procName))
                        continue;

                    // Check app whitelist
                    if (!IsAppWhitelisted(procName))
                    {
                        // Don't fire violation for same process within 5 seconds
                        if (procName == lastViolationProcess &&
                            (DateTime.Now - lastViolationTime).TotalSeconds < 5)
                            continue;

                        lastViolationProcess = procName;
                        lastViolationTime = DateTime.Now;
                        var title = GetWindowTitle(hWnd);
                        ViolationDetected?.Invoke(proc, title);
                        continue;
                    }

                    // Check if it's a browser and enforce URL whitelist
                    if (_enforceUrls && IsBrowserApp(procName))
                    {
                        var url = _urlDetector.GetCurrentUrl(hWnd);
                        if (!string.IsNullOrEmpty(url) && !IsUrlWhitelisted(url))
                        {
                            if (procName == lastViolationProcess &&
                                (DateTime.Now - lastViolationTime).TotalSeconds < 5)
                                continue;

                            lastViolationProcess = procName;
                            lastViolationTime = DateTime.Now;
                            UrlViolationDetected?.Invoke(proc, url, GetWindowTitle(hWnd));
                        }
                    }
                }
                catch (ArgumentException) { } // Process exited
                catch (InvalidOperationException) { }
            }
            catch (TaskCanceledException) { break; }
            catch { }
        }
    }

    public bool IsAppWhitelisted(string processName)
    {
        return _appWhitelist.Any(a =>
            a.IsEnabled &&
            a.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsBrowserApp(string processName)
    {
        return _appWhitelist.Any(a =>
            a.IsEnabled &&
            a.IsBrowser &&
            a.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsUrlWhitelisted(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return _urlWhitelist.Any(u => u.IsEnabled && u.Matches(url));
    }

    private static bool IsSystemProcess(string name)
    {
        var systemProcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "explorer", "SearchApp", "StartMenuExperienceHost", "ShellExperienceHost",
            "TextInputHost", "ApplicationFrameHost", "SystemSettings", "Taskmgr",
            "sihost", "svchost", "csrss", "smss", "wininit", "services",
            "lsass", "winlogon", "spoolsv", "dwm", "RuntimeBroker",
            "SecurityHealthSystray", "SecurityHealthService", "SgrmBroker",
            "ctfmon", "fontdrvhost", "WmiPrvSE", "conhost", "OpenWith"
        };
        return systemProcs.Contains(name);
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        var length = NativeMethods.GetWindowTextLength(hWnd);
        if (length == 0) return "";
        var sb = new System.Text.StringBuilder(length + 1);
        NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public void Dispose()
    {
        Stop();
        _urlDetector.Dispose();
    }
}
