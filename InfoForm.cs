namespace ErgoWattControl;

/// <summary>☰ → Info: version of this build, links to the project page, new versions and the help, and where the data is.</summary>
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

    private static LinkLabel Link(string text, string url)
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
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception) { /* no browser registered */ }
        };
        return link;
    }
}
