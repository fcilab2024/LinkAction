using LinkAction.Core;
using Xunit;

namespace LinkAction.Tests;

public sealed class LinkRequestTests
{
    private const string Marker = "https://linkprotect.cudasvc.com/url?a=";

    [Fact]
    public void ExtractsTheHostFromTheFirstDestinationAndPreservesTheWrapper()
    {
        const string url = "HTTPS://LINKPROTECT.CUDASVC.COM/URL?A=" +
            "https%3A%2F%2Ffirst.example%3A8443%2Fpath%3Fx%3D1%26y%3D2" +
            "&a=https%3A%2F%2Fsecond.example#fragment";

        var request = new LinkRequest(url);

        Assert.True(request.IsBarracuda);
        Assert.Equal("first.example", request.TargetHost);
        Assert.Equal(url, request.Url);
    }

    [Theory]
    [InlineData("https://target.example/path", "target.example")]
    [InlineData("https%3A%2F%2Ftarget.example%2Fpath", "target.example")]
    [InlineData("https%253A%252F%252Ftarget.example%252Fpath", "")]
    public void DecodesTheDestinationOnlyOnce(string destination, string expectedHost)
    {
        Assert.Equal(expectedHost, new LinkRequest(Marker + destination).TargetHost);
    }

    [Theory]
    [InlineData("&a=https://second.example")]
    [InlineData("urn:example:document")]
    [InlineData("#https://fragment.example")]
    public void LeavesTargetHostEmptyWhenTheFirstDestinationHasNoHost(string queryTail)
    {
        Assert.Empty(new LinkRequest(Marker + queryTail).TargetHost);
    }
}
