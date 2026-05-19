using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

internal static class Program
{
    private static readonly string AppDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LinkAction");
    private static readonly string BrowserFile = Path.Combine(AppDir, "browser.txt");
    private static readonly string LogFile = Path.Combine(AppDir, "log.txt");
    private static readonly string AllowlistFile = Path.Combine(AppDir, "allowlist.txt");

    // Only show logic for Barracuda-protected URLs
    private const string CudaFilter = "https://linkprotect.cudasvc.com/url?a=";

    [STAThread]
    [SupportedOSPlatform("windows")]
    private static async Task Main(string[] args)
    {
        try
        {
            Directory.CreateDirectory(AppDir);
            EnsureAllowlistTemplateExists();

            Log("=== LinkAction started ===");
            Log($"Args count={args.Length}; Args={string.Join(" | ", args)}");
            var tenantId = Microsoft.Win32.Registry.LocalMachine
                .OpenSubKey(@"SOFTWARE\LinkAction")
                ?.GetValue("TenantId")?.ToString()
                ?? Environment.GetEnvironmentVariable("CLICKGUARD_TENANT_ID");

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                Log("WARNING: TenantId not found in the Registry or in the environment variable.");
                Log(" The ClickGuard remote allowlist will NOT be consulted in this session.");
                Log(" Run the correct .reg file for this company and restart.");
            }
            else
            {
                Log($" TenantId detectado: {tenantId}");
            }

            if (args.Length == 0)
            {
                Log("No URL argument. Exiting.");
                return;
            }

            var raw = args[0].Trim('"', ' ');
            Log($"Raw arg[0]={raw}");

            if (string.IsNullOrWhiteSpace(raw))
            {
                Log("Empty URL argument. Exiting.");
                return;
            }

            var url = NormalizeUrl(raw);
            Log($"Normalized URL={url}");

            if (string.IsNullOrWhiteSpace(url))
            {
                Log("Not a valid http/https URL. Exiting.");
                return;
            }

            // 1) Only care about Barracuda-protected URLs at all.
            if (!url.Contains(CudaFilter, StringComparison.OrdinalIgnoreCase))
            {
                Log("URL does not match CudaFilter. Forwarding without prompt.");
                OpenInRealBrowser(url);
                return;
            }

            // 2) For Cuda URLs, only prompt when launched from Outlook / olk / etc.
            bool fromOutlook = IsCallerOutlook();
            Log($"IsCallerOutlook={fromOutlook}");
            if (!fromOutlook)
            {
                Log("Cuda URL but caller is not Outlook. Forwarding without prompt.");
                OpenInRealBrowser(url);
                return;
            }

            // 3) Resolve the real target host for allowlist + display
            var targetHost = GetTargetHost(url);
            Log($"Resolved target host: {targetHost}");

            // 4) Verifica allowlist local E remota (Azure ClickGuard)
            if (!string.IsNullOrEmpty(targetHost))
            {
                bool localAllowed = IsAllowedDomain(targetHost);
                bool remoteAllowed = await AzureApiService.IsAllowedDomainAsync(targetHost);

                if (localAllowed || remoteAllowed)
                {
                    Log($"Host '{targetHost}' liberado (local={localAllowed}, remoto={remoteAllowed}). Abrindo.");
                    OpenInRealBrowser(url);
                    return;
                }
            }

            // 5) Build friendly display text for popup
            var displayTarget = GetDisplayTarget(url, targetHost);
            Log($"Display target: {displayTarget}");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Build the text you already have
            var displayText = $"Are you sure you want to open this link?\n\n{displayTarget}";

            // Show our tiny custom dialog with the embedded logo
            DialogResult result;
            using (var dlg = new ConfirmDialog(displayText, LoadEmbeddedLogo()))
            {
                result = dlg.ShowDialog();
            }

            Log($"User clicked: {result}");

            if (result == DialogResult.Yes)
            {
                Log("User confirmed. Opening in real browser.");
                OpenInRealBrowser(url);
            }
            else
            {
                Log("User declined. Not opening URL.");
            }


            Log($"User clicked: {result}");

            if (result == DialogResult.Yes)
            {
                Log("User confirmed. Opening in real browser.");
                OpenInRealBrowser(url);
            }
            else
            {
                Log("User declined. Not opening URL.");
            }
        }
        catch (Exception ex)
        {
            try { Log("ERROR: " + ex); } catch { /* ignore */ }
        }
    }

    // ----- URL helpers -----

    private static string NormalizeUrl(string input)
    {
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return input;
        }
        return string.Empty;
    }

    /// <summary>
    /// Extract the real target host from Barracuda SafeLink (a= param).
    /// Returns null/empty if it cannot be determined.
    /// </summary>
    private static string GetTargetHost(string url)
    {
        try
        {
            var outer = new Uri(url);

            if (!outer.Host.Contains("linkprotect.cudasvc.com", StringComparison.OrdinalIgnoreCase))
                return outer.Host ?? string.Empty;

            var query = outer.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in query)
            {
                var idx = part.IndexOf('=');
                if (idx <= 0) continue;

                var key = part[..idx];
                var val = part[(idx + 1)..];

                if (!key.Equals("a", StringComparison.OrdinalIgnoreCase))
                    continue;

                var decoded = Uri.UnescapeDataString(val);

                if (Uri.TryCreate(decoded, UriKind.Absolute, out var inner))
                    return inner.Host ?? string.Empty;

                return string.Empty;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            Log("GetTargetHost error: " + ex.Message);
            return string.Empty;
        }
    }

    /// <summary>
    /// Human-friendly text for the popup.
    /// Prefer the resolved host; fall back to original URL.
    /// </summary>
    private static string GetDisplayTarget(string originalUrl, string targetHost)
    {
        if (!string.IsNullOrWhiteSpace(targetHost))
            return targetHost;

        return originalUrl;
    }

    // ----- Allowlist (file-based) -----

    private static void EnsureAllowlistTemplateExists()
    {
        try
        {
            if (!File.Exists(AllowlistFile))
            {
                var template = new[]
                {
                    "# LinkAction allowlist",
                    "# One domain per line.",
                    "# Examples:",
                    "# yourcompany.com",
                    "# corp.yourcompany.com",
                    "# intranet.yourcompany.local",
                    ""
                };
                File.WriteAllLines(AllowlistFile, template);
                Log("Created allowlist template file.");
            }
        }
        catch
        {
            // Non-fatal; if we can't create it, we just won't use allowlist.
        }
    }

    private static bool IsAllowedDomain(string host)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(host))
                return false;

            host = host.ToLowerInvariant();

            if (!File.Exists(AllowlistFile))
                return false;

            var lines = File.ReadAllLines(AllowlistFile);

            foreach (var raw in lines)
            {
                var d = raw.Trim();

                // Skip comments and blanks
                if (string.IsNullOrEmpty(d) || d.StartsWith("#"))
                    continue;

                d = d.ToLowerInvariant();

                // Exact host or subdomain match
                if (host == d || host.EndsWith("." + d, StringComparison.Ordinal))
                    return true;
            }
        }
        catch (Exception ex)
        {
            Log("IsAllowedDomain error: " + ex.Message);
        }

        return false;
    }

    // ----- Browser launching -----

    private static void OpenInRealBrowser(string url)
    {
        var browserPath = GetPreferredBrowserPath();

        if (!string.IsNullOrWhiteSpace(browserPath) && File.Exists(browserPath))
        {
            Log($"Launching real browser: {browserPath} {url}");
            Process.Start(new ProcessStartInfo
            {
                FileName = browserPath,
                Arguments = $"\"{url}\"",
                UseShellExecute = false
            });
        }
        else
        {
            Log("No explicit browser found; using system default.");
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }

    private static string GetPreferredBrowserPath()
    {
        // Optional override: %LOCALAPPDATA%\LinkAction\browser.txt
        try
        {
            if (File.Exists(BrowserFile))
            {
                var p = File.ReadAllText(BrowserFile).Trim();
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                {
                    Log($"Using browser from browser.txt: {p}");
                    return p;
                }
            }
        }
        catch (Exception ex)
        {
            Log("GetPreferredBrowserPath error: " + ex.Message);
        }

        var candidates = new[]
        {
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Mozilla Firefox\firefox.exe"
        };

        var found = candidates.FirstOrDefault(File.Exists) ?? string.Empty;
        if (!string.IsNullOrEmpty(found))
            Log($"Detected browser: {found}");

        return found;
    }

    // ----- Outlook caller detection via Toolhelp snapshot -----

    private static bool IsCallerOutlook()
    {
        try
        {
            using var current = Process.GetCurrentProcess();
            int pid = current.Id;

            // Walk up to 5 levels of parent processes
            for (int depth = 0; depth < 5; depth++)
            {
                int parentId = GetParentProcessId(pid);
                if (parentId <= 0)
                    break;

                using var parent = Process.GetProcessById(parentId);
                var name = parent.ProcessName.ToLowerInvariant();
                Log($"Ancestor[{depth}]: {name} (PID={parentId})");

                if (IsOutlookProcessName(name))
                    return true;

                pid = parentId;
            }
        }
        catch (Exception ex)
        {
            Log("IsCallerOutlook error: " + ex.Message);
        }

        return false;
    }

    private static bool IsOutlookProcessName(string name)
    {
        return name.Contains("outlook")
               || name.Contains("hxoutlook")
               || name == "olk"
               || name.Contains("microsoft.outlook");
    }

    // ----- P/Invoke: get parent process ID -----

    private static int GetParentProcessId(int pid)
    {
        const uint TH32CS_SNAPPROCESS = 0x00000002;

        IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == IntPtr.Zero || snapshot.ToInt64() == -1)
            return 0;

        try
        {
            PROCESSENTRY32 procEntry = new PROCESSENTRY32();
            procEntry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));

            if (!Process32First(snapshot, ref procEntry))
                return 0;

            do
            {
                if (procEntry.th32ProcessID == pid)
                    return (int)procEntry.th32ParentProcessID;
            }
            while (Process32Next(snapshot, ref procEntry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll")]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll")]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    // ----- Logging -----

    private static void Log(string message)
    {
        File.AppendAllText(LogFile, $"{DateTime.Now:O} {message}{Environment.NewLine}");
    }

// Loads the embedded PNG (see embedding step below)
private static Image? LoadEmbeddedLogo()
{
    try
    {
        const string resourceName = "LinkAction.logo.png"; // LogicalName in .csproj
        var asm = Assembly.GetExecutingAssembly();
        using var s = asm.GetManifestResourceStream(resourceName);
        if (s == null) return null;
        return Image.FromStream(s);
    }
    catch { return null; }
}

    // Lightweight dialog that mimics MessageBox with Yes/No but shows a logo
    internal sealed class ConfirmDialog : Form
    {
        public ConfirmDialog(string message, Image? logo)
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
                Image = logo ?? SystemIcons.Warning.ToBitmap()
            };

            var title = new Label
            {
                AutoSize = true,
                Text = "Are you sure you want to open this link?",
                Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                ForeColor = Color.Black,
                Margin = new Padding(0, 4, 0, 8)
            };

            var body = new Label
            {
                AutoSize = true,
                Text = message.Contains("\n\n") ? message.Split("\n\n")[1] : message, // show target on its own line
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, SystemFonts.MessageBoxFont.Size + 1),
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
}

