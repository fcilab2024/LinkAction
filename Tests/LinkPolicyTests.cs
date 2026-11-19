using LinkAction.Core;
using Xunit;

namespace LinkAction.Tests;

public sealed class LinkPolicyTests
{
    private const string Marker = "https://linkprotect.cudasvc.com/url?a=";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \" \"")]
    [InlineData("example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("file:///document")]
    public void RejectsMissingOrUnsupportedArgument(string? argument)
    {
        Assert.Equal(LinkDecision.Reject, LinkPolicy.Evaluate(new(argument), true, ["example.com"]));
    }

    [Theory]
    [InlineData("http://example.com", "http://example.com")]
    [InlineData(" \"HTTPS://Example.com/a%2Fb?q=1#here\" ", "HTTPS://Example.com/a%2Fb?q=1#here")]
    [InlineData("http://", "http://")] // Legacy validation checks the prefix only.
    [InlineData("https://linkprotect.cudasvc.com/url?x=1&a=https://example.com",
        "https://linkprotect.cudasvc.com/url?x=1&a=https://example.com")]
    public void ForwardsUnmarkedUrlsWithoutRewriting(string argument, string expectedUrl)
    {
        var request = new LinkRequest(argument);
        Assert.Equal(expectedUrl, request.Url);
        Assert.Equal(LinkDecision.OpenDirectly, LinkPolicy.Evaluate(request, true, []));
    }

    [Theory]
    [InlineData("trusted.example", LinkDecision.OpenDirectly)]
    [InlineData("a.b.trusted.example", LinkDecision.OpenDirectly)]
    [InlineData("untrusted.example", LinkDecision.RequireConfirmation)]
    [InlineData("trusted.example.evil.test", LinkDecision.RequireConfirmation)]
    [InlineData("comment.example", LinkDecision.RequireConfirmation)]
    public void MatchesOnlyListedDomainsAndTheirSubdomains(string host, LinkDecision expected)
    {
        var request = new LinkRequest(Marker + Uri.EscapeDataString($"https://{host}:443/path?x=1&y=2"));
        Assert.Equal(host, request.TargetHost);
        Assert.Equal(expected, LinkPolicy.Evaluate(request, true, ["  ", " # comment.example", " TRUSTED.Example "]));
    }

    [Fact]
    public void PreservesCaseInsensitiveMarkerAndOuterHostFallback()
    {
        var request = new LinkRequest("https://outer.example/?next=HTTPS://LINKPROTECT.CUDASVC.COM/URL?A=bad");
        Assert.Equal("outer.example", request.TargetHost);
        Assert.Equal(LinkDecision.RequireConfirmation, LinkPolicy.Evaluate(request, true, []));
        Assert.Equal(LinkDecision.OpenDirectly, LinkPolicy.Evaluate(request, true, ["outer.example"]));
    }

    [Theory]
    [InlineData(Marker)]
    [InlineData(Marker + "not-an-absolute-uri")]
    [InlineData(Marker + "%2Frelative")]
    [InlineData(Marker + "bad&a=https://trusted.example")]
    [InlineData("http://[invalid]/?next=" + Marker)]
    public void UnresolvedHostRequiresConfirmationOnlyForOutlook(string url)
    {
        var request = new LinkRequest(url);
        Assert.Empty(request.TargetHost);
        Assert.Equal(LinkDecision.RequireConfirmation, LinkPolicy.Evaluate(request, true, ["", "trusted.example"]));
        Assert.Equal(LinkDecision.OpenDirectly, LinkPolicy.Evaluate(request, false, []));
    }

    [Theory]
    [InlineData("OUTLOOK", true)]
    [InlineData("HxOutlook", true)]
    [InlineData("Microsoft.Outlook.Exe", true)]
    [InlineData("olk", true)]
    [InlineData("OLKHelper", false)]
    [InlineData("explorer", false)]
    public void RecognizesExistingOutlookNamePatterns(string name, bool expected)
    {
        Assert.Equal(expected, LinkPolicy.IsOutlookCaller([name]));
    }

    [Fact]
    public void OnlyTheFirstFiveAncestorsCount()
    {
        Assert.True(LinkPolicy.IsOutlookCaller(["a", "b", "c", "d", "olk"]));
        Assert.False(LinkPolicy.IsOutlookCaller(["a", "b", "c", "d", "e", "olk"]));
        Assert.False(LinkPolicy.IsOutlookCaller([]));
    }

    [Fact]
    public void IdenticalInputsGiveTheSameDecisionAcrossInterveningCalls()
    {
        const string url = Marker + "https%3A%2F%2Foutside.example";
        string[] domains = ["trusted.example"];
        var first = LinkPolicy.Evaluate(new(url), true, domains);
        Assert.Equal(LinkDecision.RequireConfirmation, first);
        Assert.Equal(LinkDecision.OpenDirectly, LinkPolicy.Evaluate(new(url), true, ["outside.example"]));
        Assert.Equal(first, LinkPolicy.Evaluate(new(url), true, domains));
    }
}
