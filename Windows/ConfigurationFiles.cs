namespace LinkAction.Windows;

internal sealed class ConfigurationFiles(string directory, Action<string> log)
{
    private readonly string allowlistFile = Path.Combine(directory, "allowlist.txt");
    private readonly string browserFile = Path.Combine(directory, "browser.txt");

    public void Initialize()
    {
        Directory.CreateDirectory(directory);
        try
        {
            if (File.Exists(allowlistFile))
                return;

            // CreateNew keeps concurrent first runs from overwriting an existing file.
            using (var stream = new FileStream(allowlistFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine("# LinkAction allowlist");
                writer.WriteLine("# One domain per line.");
                writer.WriteLine("# Examples:");
                writer.WriteLine("# yourcompany.com");
                writer.WriteLine("# corp.yourcompany.com");
                writer.WriteLine("# intranet.yourcompany.local");
                writer.WriteLine();
            }
            log("Created allowlist template file.");
        }
        catch (Exception ex)
        {
            log("Allowlist template creation skipped: " + ex.Message);
        }
    }

    public IReadOnlyList<string> ReadAllowlist()
    {
        try
        {
            if (File.Exists(allowlistFile))
                return File.ReadAllLines(allowlistFile);
        }
        catch (Exception ex)
        {
            log("Allowlist read error: " + ex.Message);
        }

        return [];
    }

    public string? ReadBrowserOverride()
    {
        try
        {
            if (File.Exists(browserFile))
                return File.ReadAllText(browserFile).Trim();
        }
        catch (Exception ex)
        {
            log("Browser override read error: " + ex.Message);
        }

        return null;
    }
}
