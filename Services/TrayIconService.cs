using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WpfApp1.Services;

/// <summary>
/// System tray icon using Shell_NotifyIcon P/Invoke — no WinForms dependency.
/// </summary>
public class TrayIconService : IDisposable
{
    private const int WM_TRAYICON = 0x8000;
    private const int NIM_ADD = 0;
    private const int NIM_DELETE = 2;
    private const int NIF_MESSAGE = 1;
    private const int NIF_ICON = 2;
    private const int NIF_TIP = 4;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x0010;

    private readonly Window _owner;
    private readonly HwndSource _hwndSource;
    private readonly IntPtr _iconHandle;
    private string _showText;
    private string _stopText;
    private string _exitText;
    private bool _visible;

    public event Action? ShowRequested;
    public event Action? StopRequested;
    public event Action? ExitRequested;

    public TrayIconService(Window owner, string iconPath, string showText, string stopText, string exitText)
    {
        _owner = owner;
        _showText = showText;
        _stopText = stopText;
        _exitText = exitText;

        _iconHandle = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE);

        var helper = new WindowInteropHelper(owner);
        var hwnd = helper.EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(hwnd)!;
        _hwndSource.AddHook(WndProc);
    }

    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value) return;
            _visible = value;
            if (value) AddTrayIcon();
            else RemoveTrayIcon();
        }
    }

    public void UpdateMenuTexts(string showText, string stopText, string exitText)
    {
        _showText = showText;
        _stopText = stopText;
        _exitText = exitText;
    }

    private void AddTrayIcon()
    {
        var hwnd = new WindowInteropHelper(_owner).Handle;
        var data = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _iconHandle,
            szTip = "Focus Mode"
        };
        Shell_NotifyIcon(NIM_ADD, ref data);
    }

    private void RemoveTrayIcon()
    {
        var hwnd = new WindowInteropHelper(_owner).Handle;
        var data = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = hwnd,
            uID = 1
        };
        Shell_NotifyIcon(NIM_DELETE, ref data);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            if (lParam.ToInt32() == WM_LBUTTONDBLCLK)
            {
                ShowRequested?.Invoke();
                handled = true;
            }
            else if (lParam.ToInt32() == WM_RBUTTONUP)
            {
                ShowContextMenu();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private void ShowContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var showItem = new System.Windows.Controls.MenuItem { Header = _showText };
        showItem.Click += (_, _) => ShowRequested?.Invoke();
        menu.Items.Add(showItem);

        var stopItem = new System.Windows.Controls.MenuItem { Header = _stopText };
        stopItem.Click += (_, _) =>
        {
            StopRequested?.Invoke();
            ShowRequested?.Invoke();
        };
        menu.Items.Add(stopItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = _exitText };
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(exitItem);

        menu.IsOpen = true;
    }

    public void Dispose()
    {
        if (_visible) RemoveTrayIcon();
        _hwndSource.RemoveHook(WndProc);
        if (_iconHandle != IntPtr.Zero) DestroyIcon(_iconHandle);
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType,
        int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersionOrTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }
}
