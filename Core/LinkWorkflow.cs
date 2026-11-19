namespace LinkAction.Core;

/// <summary>Collects input on demand and applies one policy result to the original URL.</summary>
public sealed class LinkWorkflow(
    Func<IReadOnlyList<string>> readAncestors,
    Func<IReadOnlyList<string>> readAllowlist,
    Func<string, bool> confirm,
    Action<string> launch,
    Action<string> log)
{
    public void Run(string? argument)
    {
        var request = new LinkRequest(argument);
        var fromOutlook = request.IsBarracuda && LinkPolicy.IsOutlookCaller(readAncestors());
        var allowlist = fromOutlook && request.TargetHost.Length > 0 ? readAllowlist() : [];
        var decision = LinkPolicy.Evaluate(request, fromOutlook, allowlist);

        log($"Decision={decision} URL={request.Url}");
        if (request.IsBarracuda)
            log($"IsCallerOutlook={fromOutlook} TargetHost={request.TargetHost}");

        switch (decision)
        {
            case LinkDecision.Reject:
                return;
            case LinkDecision.RequireConfirmation:
                var displayTarget = request.TargetHost.Length > 0 ? request.TargetHost : request.Url;
                if (!confirm(displayTarget))
                {
                    log("User declined. Not opening URL.");
                    return;
                }
                log("User confirmed. Opening in real browser.");
                break;
            case LinkDecision.OpenDirectly:
                break;
            default:
                throw new InvalidOperationException($"Unknown link decision {decision}.");
        }

        launch(request.Url);
    }
}
