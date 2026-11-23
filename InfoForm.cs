namespace ErgoWattControl;

/// <summary>☰ → Info: version of this build, links to the project page, new versions, the help and the log, and where the data is.</summary>
public sealed class InfoForm : Form
{
    public InfoForm()
    {
        Text = L.T("Info_Title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(20, 16, 20, 12);
        Font = new Font("Segoe UI", 10);

        var icon = new PictureBox
        {
            Image = AppIcon.Value is { } appIcon ? new Icon(appIcon, 64, 64).ToBitmap() : null,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(64, 64),
            Margin = new Padding(0, 4, 16, 0),
        };
        var text = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Margin = Padding.Empty };
        text.Controls.Add(new Label { Text = "ErgoWattControl", AutoSize = true, Font = new Font("Segoe UI", 16, FontStyle.Bold) });
        text.Controls.Add(new Label { Text = L.F("Info_Version", AppInfo.Version), AutoSize = true, Margin = new Padding(3, 2, 3, 8) });
        text.Controls.Add(new Label { Text = L.T("Info_Subtitle"), AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 0, 3, 12) });
        text.Controls.Add(Link(L.T("Info_Project"), AppInfo.ProjectUrl));
        text.Controls.Add(Link(L.T("Info_Releases"), AppInfo.ProjectUrl + "/releases/latest"));
        text.Controls.Add(Link(L.T("Info_HelpOnline"), L.T("Info_HelpUrl")));
        var logRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        logRow.Controls.Add(Link(L.T("Info_OpenLog"), AppPaths.LogFile, OpenLog));
        logRow.Controls.Add(Link(L.T("Info_OpenFolder"), AppPaths.SettingsFolder, OpenFolder));
        text.Controls.Add(logRow);
        text.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 12, 3, 0),
            Text = L.F("Info_Folders", AppPaths.SettingsFolder, AppPaths.DataFolder) + (AppPaths.Portable ? L.T("Info_Portable") : ""),
        });

        var ok = new Button { Text = L.T("Button_Ok"), DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(90, 0), Anchor = AnchorStyles.Right, Margin = new Padding(0, 16, 0, 0) };
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        layout.Controls.Add(icon, 0, 0);
        layout.Controls.Add(text, 1, 0);
        layout.Controls.Add(ok, 1, 1);
        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = ok;
    }

    /// <summary>
    /// Opens the log in a text editor: Notepad++ if installed (it notices when the running app appends), else Notepad.
    /// Without a log yet (nothing went wrong so far), the folder opens instead.
    /// </summary>
    private static void OpenLog()
    {
        var log = AppPaths.LogFile;
        if (!File.Exists(log)) { OpenFolder(); return; }
        Start(new System.Diagnostics.ProcessStartInfo(LogEditor(), $"\"{log}\""));
    }

    /// <summary>Opens the folder of the log and settings.json in Explorer.</summary>
    private static void OpenFolder()
    {
        try { Directory.CreateDirectory(AppPaths.SettingsFolder); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{AppPaths.SettingsFolder}\""));
    }

    private static void Start(System.Diagnostics.ProcessStartInfo start)
    {
        try { System.Diagnostics.Process.Start(start); }
        catch (System.ComponentModel.Win32Exception) { }
    }

    /// <summary>The editor for the log: Notepad++ (registered in App Paths or in Program Files), else Notepad.</summary>
    internal static string LogEditor()
    {
        foreach (var root in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
        {
            using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\notepad++.exe");
            if (key?.GetValue(null) is string path && File.Exists(path.Trim('"')))
                return path.Trim('"');
        }
        foreach (var folder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
        {
            var path = Path.Combine(Environment.GetFolderPath(folder), "Notepad++", "notepad++.exe");
            if (File.Exists(path))
                return path;
        }
        return "notepad.exe";
    }

    private static LinkLabel Link(string text, string url, Action? open = null)
    {
        var link = new LinkLabel
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(3, 2, 3, 2),
            LinkColor = Color.FromArgb(110, 170, 255),
            ActiveLinkColor = Color.FromArgb(150, 200, 255),
            VisitedLinkColor = Color.FromArgb(110, 170, 255),
        };
        new ToolTip().SetToolTip(link, url);
        link.LinkClicked += (_, _) =>
        {
            if (open is not null) { open(); return; }
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception) { /* no browser registered */ }
        };
        return link;
    }
}
