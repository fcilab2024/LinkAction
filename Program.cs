using LinkAction.Core;
using LinkAction.Windows;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LinkAction");
        var log = new DiagnosticLog(Path.Combine(directory, "log.txt"));
        try
        {
            var configuration = new ConfigurationFiles(directory, log.Write);
            configuration.Initialize();
            log.Write("=== LinkAction started ===");
            log.Write($"Args count={args.Length}; Args={string.Join(" | ", args)}");

            var ancestry = new ProcessAncestry(log.Write);
            var browser = new BrowserLauncher(configuration, log.Write);
            var workflow = new LinkWorkflow(ancestry.ReadNames, configuration.ReadAllowlist,
                ConfirmationPrompt.Show, browser.Open, log.Write);
            workflow.Run(args.FirstOrDefault());
        }
        catch (Exception ex)
        {
            log.ReportFailure(ex);
        }
    }
}
