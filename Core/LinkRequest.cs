namespace LinkAction.Core;

/// <summary>The first handler argument, with the original URL spelling kept for launch and display.</summary>
public sealed class LinkRequest
{
    private const string BarracudaMarker = "https://linkprotect.cudasvc.com/url?a=";

    public string Url { get; }
    public bool IsHttpUrl { get; }
    public bool IsBarracuda { get; }
    public string TargetHost { get; }

    public LinkRequest(string? argument)
    {
        Url = argument?.Trim('"', ' ') ?? string.Empty;
        IsHttpUrl = Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        IsBarracuda = IsHttpUrl && Url.Contains(BarracudaMarker, StringComparison.OrdinalIgnoreCase);
        TargetHost = IsBarracuda ? ExtractTargetHost(Url) : string.Empty;
    }

    private static string ExtractTargetHost(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var outer))
            return string.Empty;

        // The marker can also occur inside another URL. Preserve the outer-host fallback.
        if (!outer.Host.Contains("linkprotect.cudasvc.com", StringComparison.OrdinalIgnoreCase))
            return outer.Host;

        foreach (var part in outer.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!part.StartsWith("a=", StringComparison.OrdinalIgnoreCase))
                continue;

            var destination = Uri.UnescapeDataString(part[2..]);
            return Uri.TryCreate(destination, UriKind.Absolute, out var inner) ? inner.Host : string.Empty;
        }

        return string.Empty;
    }
}
