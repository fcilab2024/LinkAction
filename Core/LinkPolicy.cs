namespace LinkAction.Core;

public enum LinkDecision
{
    Reject,
    OpenDirectly,
    RequireConfirmation
}

public static class LinkPolicy
{
    public const int MaxAncestors = 5;

    public static bool IsOutlookCaller(IReadOnlyList<string> ancestorNames) =>
        ancestorNames.Take(MaxAncestors).Any(name =>
            name.Contains("outlook", StringComparison.OrdinalIgnoreCase)
            || name.Equals("olk", StringComparison.OrdinalIgnoreCase));

    public static LinkDecision Evaluate(LinkRequest request, bool fromOutlook, IReadOnlyList<string> allowlist)
    {
        if (!request.IsHttpUrl)
            return LinkDecision.Reject;

        if (!request.IsBarracuda || !fromOutlook || IsAllowlisted(request.TargetHost, allowlist))
            return LinkDecision.OpenDirectly;

        return LinkDecision.RequireConfirmation;
    }

    private static bool IsAllowlisted(string host, IReadOnlyList<string> allowlist)
    {
        if (host.Length == 0)
            return false;

        host = host.ToLowerInvariant();
        foreach (var line in allowlist)
        {
            var domain = line.Trim().ToLowerInvariant();
            if (domain.Length == 0 || domain.StartsWith('#'))
                continue;

            if (host == domain || host.EndsWith("." + domain, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
