using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace WpfApp1.Services;

/// <summary>
/// Detects the current URL from browser windows using managed UI Automation.
/// Falls back to window title parsing.
/// </summary>
public class BrowserUrlDetector : IDisposable
{
    public string GetCurrentUrl(IntPtr browserHwnd)
    {
        // Strategy 1: Try reading from window title for browser tab info
        var title = GetWindowTitle(browserHwnd);
        var url = ExtractUrlFromTitle(title);
        if (!string.IsNullOrEmpty(url))
            return url;

        // Strategy 2: Try managed UI Automation to read address bar
        return GetUrlViaUIA(browserHwnd);
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        var length = NativeMethods.GetWindowTextLength(hWnd);
        if (length == 0) return "";
        var sb = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string ExtractUrlFromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";

        var suffixes = new[]
        {
            " - Microsoft Edge",
            " - Google Chrome",
            " - Mozilla Firefox",
            " - Profile",
            " - Work",
            " - Personal"
        };

        foreach (var suffix in suffixes)
        {
            var idx = title.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                title = title[..idx].Trim();
        }

        if (title.Contains("://") || title.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            return title;

        return "";
    }

    private static string GetUrlViaUIA(IntPtr browserHwnd)
    {
        try
        {
            var element = AutomationElement.FromHandle(browserHwnd);
            if (element == null) return "";

            // Find all Edit controls (address bar is an edit control)
            var edits = element.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));

            foreach (AutomationElement edit in edits)
            {
                try
                {
                    // Try Value pattern (most reliable for address bar)
                    if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out object? valueObj) && valueObj is ValuePattern vp)
                    {
                        var val = vp.Current.Value ?? "";
                        if (IsLikelyUrl(val))
                            return val;
                    }
                }
                catch { }

                try
                {
                    // Try Name property as fallback
                    var name = edit.Current.Name ?? "";
                    if (IsLikelyUrl(name))
                        return name;
                }
                catch { }
            }

            // Also try Document controls (web content area may expose URL)
            var docs = element.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
            foreach (AutomationElement doc in docs)
            {
                try
                {
                    var name = doc.Current.Name ?? "";
                    if (IsLikelyUrl(name))
                        return name;
                }
                catch { }
            }
        }
        catch
        {
            // UIA not available
        }

        return "";
    }

    private static bool IsLikelyUrl(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text.Length > 2000) return false;
        return text.Contains("://")
            || text.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            || text.Contains(".com/") || text.Contains(".org/") || text.Contains(".net/")
            || text.Contains(".cn/") || text.Contains(".io/") || text.Contains(".dev/")
            || text.Contains(".com") && !text.Contains(' ');
    }

    public void Dispose() { }
}
