namespace LinkAction.Windows;

internal static class ConfirmationPrompt
{
    public static bool Show(string target)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var logo = LoadLogo() ?? SystemIcons.Warning.ToBitmap();
        using var dialog = new ConfirmDialog(target, logo);
        return dialog.ShowDialog() == DialogResult.Yes;
    }

    private static Bitmap? LoadLogo()
    {
        try
        {
            using var stream = typeof(ConfirmDialog).Assembly.GetManifestResourceStream("LinkAction.logo.png");
            if (stream == null)
                return null;

            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
