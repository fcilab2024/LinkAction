namespace LinkAction.Windows;

internal sealed class ConfirmDialog : Form
{
    public ConfirmDialog(string target, Image logo)
    {
        Text = "Security Warning – LinkAction";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.White;
        AutoScaleMode = AutoScaleMode.Dpi;
        Padding = new Padding(0);   // we'll pad via the panel

        var tl = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 3
        };
        tl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(tl);

        var pic = new PictureBox
        {
            Size = new Size(64, 64),
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 0, 16, 0),
            Image = logo
        };

        var messageFont = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;
        var title = new Label
        {
            AutoSize = true,
            Text = "Are you sure you want to open this link?",
            Font = new Font(messageFont, FontStyle.Bold),
            ForeColor = Color.Black,
            Margin = new Padding(0, 4, 0, 8)
        };

        var body = new Label
        {
            AutoSize = true,
            Text = target,
            Font = new Font(messageFont.FontFamily, messageFont.Size + 1),
            ForeColor = Color.FromArgb(32, 32, 32),
            Margin = new Padding(0, 0, 0, 12),
            MaximumSize = new Size(680, 0)   // wrap long domains/URLs
        };

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        var yes = new Button { Text = "Yes", DialogResult = DialogResult.Yes, AutoSize = true, Padding = new Padding(16, 6, 16, 6) };
        var no = new Button { Text = "No", DialogResult = DialogResult.No, AutoSize = true, Padding = new Padding(16, 6, 16, 6) };
        buttons.Controls.Add(yes);
        buttons.Controls.Add(no);
        AcceptButton = yes; // Enter
        CancelButton = no;  // Esc
        no.Select();        // default focus

        // Layout
        tl.Controls.Add(pic, 0, 0);
        tl.SetRowSpan(pic, 2);
        tl.Controls.Add(title, 1, 0);
        tl.Controls.Add(body, 1, 1);
        tl.Controls.Add(buttons, 1, 2);

        // --- Robust sizing: measure & clamp to screen ---
        tl.PerformLayout();
        var work = Screen.PrimaryScreen!.WorkingArea;

        // Ask the panel what size it wants if it could grow reasonably wide
        var preferred = tl.GetPreferredSize(new Size(720, 0));
        int desiredW = Math.Min(preferred.Width + 32, Math.Max(480, work.Width - 120));
        int desiredH = Math.Min(preferred.Height + 32, Math.Max(180, work.Height - 200));

        // Apply as ClientSize so borders are accounted for by WinForms
        ClientSize = new Size(desiredW, desiredH);

        // Don’t let the OS shrink us to nothing because of DPI/AutoSize quirks
        MinimumSize = new Size(480, 180);
    }
}
