using System.Text.Json.Serialization;

namespace WpfApp1.Models;

/// <summary>
/// Represents a URL pattern in the whitelist.
/// Value is a domain or URL fragment (e.g., "github.com", "stackoverflow.com").
/// </summary>
public class UrlWhitelistEntry
{
    public string Pattern { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    [JsonIgnore]
    public string DisplayText => string.IsNullOrEmpty(Description)
        ? Pattern
        : $"{Description} ({Pattern})";

    public bool Matches(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return url.Contains(Pattern, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) =>
        obj is UrlWhitelistEntry other &&
        string.Equals(other.Pattern, Pattern, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => Pattern.ToLowerInvariant().GetHashCode();
}
