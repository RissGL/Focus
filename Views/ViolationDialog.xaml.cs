using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace WpfApp1.Views;

public enum ViolationResult
{
    Ignore,
    AddToWhitelist,
    CloseApp
}

public partial class ViolationDialog : Window
{
    private readonly DispatcherTimer _countdownTimer;
    private int _countdown = 15;
    private ViolationResult _result = ViolationResult.Ignore;

    public ViolationResult Result => _result;
    public Process TargetProcess { get; }

    public ViolationDialog(Process targetProcess, string windowTitle, bool isUrlViolation = false, string? url = null)
    {
        InitializeComponent();
        TargetProcess = targetProcess;

        if (isUrlViolation && url != null)
        {
            TitleText.Text = "Non-whitelisted Website";
            SubtitleText.Text = "This website is not in your focus URL whitelist.";
            AppNameText.Text = url;
            DetailText.Text = $"Browser: {targetProcess.ProcessName}.exe — {windowTitle}";
        }
        else
        {
            TitleText.Text = "Non-whitelisted Application";
            SubtitleText.Text = "This app is not in your focus whitelist.";
            AppNameText.Text = $"{targetProcess.ProcessName}.exe";
            DetailText.Text = string.IsNullOrEmpty(windowTitle)
                ? "Window title not available"
                : windowTitle;
        }

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += CountdownTick;
        _countdownTimer.Start();
    }

    private void CountdownTick(object? sender, EventArgs e)
    {
        _countdown--;
        if (_countdown <= 0)
        {
            _countdownTimer.Stop();
            _result = ViolationResult.Ignore;
            DialogResult = true;
            Close();
        }
        else
        {
            CountdownText.Text = $"Auto-close in {_countdown} seconds...";
        }
    }

    private void IgnoreBtn_Click(object sender, RoutedEventArgs e)
    {
        _countdownTimer.Stop();
        _result = ViolationResult.Ignore;
        DialogResult = true;
        Close();
    }

    private void AddWhitelistBtn_Click(object sender, RoutedEventArgs e)
    {
        _countdownTimer.Stop();
        _result = ViolationResult.AddToWhitelist;
        DialogResult = true;
        Close();
    }

    private void CloseAppBtn_Click(object sender, RoutedEventArgs e)
    {
        _countdownTimer.Stop();
        _result = ViolationResult.CloseApp;
        DialogResult = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _countdownTimer.Stop();
        base.OnClosed(e);
    }
}
