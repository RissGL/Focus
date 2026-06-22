using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WpfApp1.Services;

/// <summary>
/// P/Invoke helpers for window manipulation and monitoring.
/// </summary>
public static class NativeMethods
{
    public const int SW_MINIMIZE = 6;
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_QUERYENDSESSION = 0x0011;
    public const uint WM_ENDSESSION = 0x0016;
    public const uint GW_OWNER = 4;
    public const int GWL_EXSTYLE = -20;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_APPWINDOW = 0x00040000;

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

    /// <summary>
    /// Determines if a window is a main application window (visible, has title, not a tool window).
    /// </summary>
    public static bool IsMainWindow(IntPtr hWnd)
    {
        if (!IsWindowVisible(hWnd)) return false;
        if (IsIconic(hWnd)) return false;

        var style = GetWindowLong(hWnd, -20); // GWL_EXSTYLE
        if ((style & WS_EX_TOOLWINDOW) != 0) return false;

        // Must have a window title
        var length = GetWindowTextLength(hWnd);
        if (length == 0) return false;

        // No owner window (owned windows are typically dialogs)
        var owner = GetWindow(hWnd, GW_OWNER);
        if (owner != IntPtr.Zero) return false;

        return true;
    }
}
