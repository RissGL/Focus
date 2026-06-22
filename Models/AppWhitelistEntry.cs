using System.Text.Json.Serialization;

namespace WpfApp1.Models;

/// <summary>
/// Represents a single app in the whitelist.
/// Value is the process name (e.g. "devenv", "chrome").
/// </summary>
public class AppWhitelistEntry
{
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsBrowser { get; set; }
    public bool IsEnabled { get; set; } = true;

    [JsonIgnore]
    public string DisplayText => string.IsNullOrEmpty(DisplayName)
        ? ProcessName
        : $"{DisplayName} ({ProcessName}.exe)";

    public override bool Equals(object? obj) =>
        obj is AppWhitelistEntry other &&
        string.Equals(other.ProcessName, ProcessName, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => ProcessName.ToLowerInvariant().GetHashCode();
}
