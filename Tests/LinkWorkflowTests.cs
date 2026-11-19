using LinkAction.Core;
using Xunit;

namespace LinkAction.Tests;

public sealed class LinkWorkflowTests
{
    private const string ProtectedUrl = "https://linkprotect.cudasvc.com/url?a=https%3A%2F%2Foutside.example";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OnlyYesLaunchesTheOriginalProtectedUrl(bool yes)
    {
        var displayed = new List<string>();
        var launched = new List<string>();
        var workflow = new LinkWorkflow(() => ["explorer", "OLK"], () => ["trusted.example"],
            target => { displayed.Add(target); return yes; }, launched.Add, _ => { });

        workflow.Run(ProtectedUrl);

        Assert.Equal("outside.example", Assert.Single(displayed));
        if (yes)
            Assert.Equal(ProtectedUrl, Assert.Single(launched));
        else
            Assert.Empty(launched);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("ftp://example.com", false)]
    [InlineData("\"http://example.com\"", true)]
    public void OrdinaryAndRejectedInputsNeedNoContextOrDialog(string? argument, bool shouldLaunch)
    {
        var launched = new List<string>();
        var workflow = new LinkWorkflow(() => throw new InvalidOperationException("Unexpected ancestry read"),
            () => throw new InvalidOperationException("Unexpected allowlist read"),
            _ => throw new InvalidOperationException("Unexpected prompt"), launched.Add, _ => { });

        workflow.Run(argument);

        if (shouldLaunch)
            Assert.Equal("http://example.com", Assert.Single(launched));
        else
            Assert.Empty(launched);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NonOutlookAndAllowlistedRequestsOpenExactlyOnce(bool outlook)
    {
        var launched = new List<string>();
        var workflow = new LinkWorkflow(() => [outlook ? "outlook" : "explorer"],
            () => outlook ? ["outside.example"] : throw new InvalidOperationException("Unexpected allowlist read"),
            _ => throw new InvalidOperationException("Unexpected prompt"), launched.Add, _ => { });

        workflow.Run(ProtectedUrl);

        Assert.Equal(ProtectedUrl, Assert.Single(launched));
    }

    [Fact]
    public void MissingTargetDisplaysTheOriginalUrl()
    {
        const string url = "https://linkprotect.cudasvc.com/url?a=bad";
        var displayed = new List<string>();
        var workflow = new LinkWorkflow(() => ["outlook"],
            () => throw new InvalidOperationException("Unexpected allowlist read"),
            target => { displayed.Add(target); return false; },
            _ => throw new InvalidOperationException("Unexpected launch"), _ => { });

        workflow.Run(url);

        Assert.Equal(url, Assert.Single(displayed));
    }

    [Fact]
    public void FailedLaunchIsNotRetried()
    {
        int attempts = 0;
        var workflow = new LinkWorkflow(() => ["outlook"], () => [], _ => true,
            _ => { attempts++; throw new InvalidOperationException("Launch failed"); }, _ => { });

        Assert.Throws<InvalidOperationException>(() => workflow.Run(ProtectedUrl));
        Assert.Equal(1, attempts);
    }
}
