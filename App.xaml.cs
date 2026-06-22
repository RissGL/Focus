using System.Windows;

namespace WpfApp1;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Ensure clean shutdown
        this.Exit += (_, _) =>
        {
            // Application-wide cleanup handled in MainWindow.OnClosing
        };
    }
}
