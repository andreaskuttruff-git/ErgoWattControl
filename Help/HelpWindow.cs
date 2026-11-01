using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ErgoWattControl;

/// <summary>
/// The help (Hilfe.html with its images, embedded in the exe) in a window of its own, shown with WebView2. The window
/// runs on its own UI thread so it stays usable while a modal dialog is open; F1 in a dialog jumps to that dialog's
/// section. Without the WebView2 runtime the help opens in the default browser instead.
/// </summary>
public static class HelpWindow
{
    // Never resolved on the network: every request to this host is answered from the embedded resources.
    private const string Host = "https://hilfe.ergowattcontrol.example/";
    private const string ResourcePrefix = "Hilfe/";

    private static readonly object Gate = new();
    private static Form? _form;          // set once the window has a handle
    private static WebView2? _view;
    private static bool _starting;
    private static string _section = "start"; // where to go once the page can be shown

    /// <summary>F1 anywhere in <paramref name="form"/> opens the help at <paramref name="section"/>.</summary>
    public static void Attach(Form form, string section) =>
        form.HelpRequested += (_, e) =>
        {
            Show(form, section);
            e.Handled = true;
        };

    /// <param name="owner">The help opens on this window's monitor.</param>
    /// <param name="section">Anchor in Hilfe.html, e.g. "strecke".</param>
    public static void Show(Control? owner, string section)
    {
        if (!RuntimeAvailable())
        {
            OpenInBrowser();
            return;
        }
        var area = (owner is null ? Screen.PrimaryScreen! : Screen.FromControl(owner)).WorkingArea;
        lock (Gate)
        {
            _section = section;
            if (_form is { } open)
            {
                open.BeginInvoke(() =>
                {
                    Navigate();
                    if (open.WindowState == FormWindowState.Minimized)
                        open.WindowState = FormWindowState.Normal;
                    open.Activate();
                });
                return;
            }
            if (_starting)
                return; // the window being created picks up _section
            _starting = true;
        }
        var thread = new Thread(() =>
        {
            var form = CreateForm(area);
            form.HandleCreated += (_, _) => { lock (Gate) { _form = form; _starting = false; } };
            form.FormClosed += (_, _) => { lock (Gate) { _form = null; _view = null; } };
            Application.Run(form);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }

    /// <summary>Closes the help window when the app ends.</summary>
    public static void Close()
    {
        lock (Gate)
        {
            if (_form is { IsDisposed: false, IsHandleCreated: true } form)
                form.BeginInvoke(form.Close);
        }
    }

    private static Form CreateForm(Rectangle area)
    {
        var size = new Size(Math.Min(1150, area.Width - 80), Math.Min(900, area.Height - 80));
        var form = new Form
        {
            Text = "ErgoWattControl – Hilfe",
            Icon = AppIcon.Value,
            StartPosition = FormStartPosition.Manual,
            Bounds = new Rectangle(area.X + (area.Width - size.Width) / 2, area.Y + (area.Height - size.Height) / 2, size.Width, size.Height),
            MinimumSize = new Size(500, 400),
        };
        var view = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(22, 22, 22) };
        form.Controls.Add(view);
        _view = view;
        form.Load += async (_, _) =>
        {
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErgoWattControl", "WebView2");
                var environment = await CoreWebView2Environment.CreateAsync(null, folder);
                await view.EnsureCoreWebView2Async(environment);
                var core = view.CoreWebView2;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.AddWebResourceRequestedFilter(Host + "*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += (_, e) => e.Response = Respond(environment, e.Request.Uri);
                // Links leaving the help (none today) go to the browser, never into this window.
                core.NavigationStarting += (_, e) =>
                {
                    if (!e.Uri.StartsWith(Host, StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        OpenUrl(e.Uri);
                    }
                };
                core.NewWindowRequested += (_, e) => e.Handled = true;
                Navigate();
            }
            catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or InvalidOperationException or System.Runtime.InteropServices.COMException)
            {
                form.Close();
                OpenInBrowser();
            }
        };
        return form;
    }

    /// <summary>Shows <see cref="_section"/>; before WebView2 is ready it is picked up at the end of loading.</summary>
    private static void Navigate()
    {
        if (_view?.CoreWebView2 is not { } core)
            return;
        string section;
        lock (Gate) { section = _section; }
        var page = Host + "Hilfe.html";
        if (core.Source.StartsWith(page, StringComparison.OrdinalIgnoreCase))
            _ = core.ExecuteScriptAsync($"document.getElementById('{section}')?.scrollIntoView()");
        else
            core.Navigate(page + "#" + section);
    }

    private static CoreWebView2WebResourceResponse Respond(CoreWebView2Environment environment, string uri)
    {
        var path = Uri.UnescapeDataString(new Uri(uri).AbsolutePath.TrimStart('/'));
        if (Resource(ResourcePrefix + path) is not { } data)
            return environment.CreateWebResourceResponse(null, 404, "Not Found", "");
        var type = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".png" => "image/png",
            _ => "application/octet-stream",
        };
        return environment.CreateWebResourceResponse(new MemoryStream(data), 200, "OK", $"Content-Type: {type}");
    }

    private static byte[]? Resource(string name)
    {
        using var stream = typeof(HelpWindow).Assembly.GetManifestResourceStream(name);
        if (stream is null)
            return null;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static bool RuntimeAvailable()
    {
        try
        {
            return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Fallback without WebView2: writes the help to the temp folder and opens it in the default browser.</summary>
    private static void OpenInBrowser()
    {
        try
        {
            var folder = Path.Combine(Path.GetTempPath(), "ErgoWattControl-Hilfe");
            foreach (var name in typeof(HelpWindow).Assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
            {
                var file = Path.Combine(folder, name[ResourcePrefix.Length..].Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllBytes(file, Resource(name)!);
            }
            OpenUrl(Path.Combine(folder, "Hilfe.html"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Die Hilfe konnte nicht geöffnet werden:\n{ex.Message}", "Hilfe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void OpenUrl(string target)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show($"Konnte nicht geöffnet werden:\n{target}\n{ex.Message}", "Hilfe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
