namespace LinkAction.Windows;

internal sealed class DiagnosticLog(string path)
{
    public void Write(string message) =>
        File.AppendAllText(path, $"{DateTime.Now:O} {message}{Environment.NewLine}");

    public void ReportFailure(Exception error)
    {
        try { Write("ERROR: " + error); }
        catch (Exception) { /* The log itself may be unavailable. */ }
    }
}
