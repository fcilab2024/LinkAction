using System.Diagnostics;

namespace LinkAction.Windows;

internal sealed class BrowserLauncher(ConfigurationFiles configuration, Action<string> log)
{
    private static readonly string[] Candidates =
    [
        @"C:\Program Files\Google\Chrome\Application\chrome.exe",
        @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        @"C:\Program Files\Mozilla Firefox\firefox.exe"
    ];

    public void Open(string url)
    {
        var browser = configuration.ReadBrowserOverride();
        if (!File.Exists(browser))
            browser = Candidates.FirstOrDefault(File.Exists);

        ProcessStartInfo startInfo;
        if (browser != null)
        {
            log($"Launching real browser: {browser} {url}");
            startInfo = new ProcessStartInfo(browser) { UseShellExecute = false };
            startInfo.ArgumentList.Add(url);
        }
        else
        {
            log("No explicit browser found; using system default.");
            startInfo = new ProcessStartInfo(url) { UseShellExecute = true };
        }

        using var process = Process.Start(startInfo);
    }
}
