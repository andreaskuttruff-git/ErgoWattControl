using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ErgoWattControl;

/// <summary>A platform the exported FIT files can be uploaded to in the <see cref="UploadWindow"/>.</summary>
/// <param name="IsLoginPage">Pages of the sign-in (the session marker is removed there, the rider signs in).</param>
/// <param name="LoginHint">Status text while signing in: how the sign-in is kept for next time.</param>
/// <param name="PageName">What the platform calls its upload page, for the status texts.</param>
/// <param name="NextStep">What is left to do on the page once the files are in.</param>
public sealed record UploadService(
    string Name, string ProfileName,
    Func<AppSettings, string> LoginUrl, Func<AppSettings, string> UploadUrl,
    Func<Uri, bool> IsLoginPage, string LoginHint, string PageName, string NextStep)
{
    public const string GarminDefaultLoginUrl = "https://sso.garmin.com/portal/sso/de-DE/sign-in?clientId=GarminConnect&service=https%3A%2F%2Fconnect.garmin.com%2Fapp";
    public const string GarminDefaultImportUrl = "https://connect.garmin.com/app/import-data";
    public const string StravaDefaultLoginUrl = "https://www.strava.com/login";
    public const string StravaDefaultUploadUrl = "https://www.strava.com/upload/select";

    public static readonly UploadService Garmin = new(
        "Garmin Connect", "Garmin",
        s => s.GarminLoginUrl, s => s.GarminImportUrl,
        uri => uri.Host.StartsWith("sso.", StringComparison.OrdinalIgnoreCase),
        "Bitte bei Garmin anmelden. „Daten speichern“ ist vorausgewählt – so geht es beim nächsten Mal direkt zur Import-Seite.",
        "Import-Seite", "jetzt auf der Seite „Daten importieren“ klicken.");

    public static readonly UploadService Strava = new(
        "Strava", "Strava",
        s => s.StravaLoginUrl, s => s.StravaUploadUrl,
        // Strava signs in on its own host (/login, /session); Google and Apple sign-in pass through their hosts.
        uri => uri.AbsolutePath.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.StartsWith("/session", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.StartsWith("/register", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.StartsWith("/oauth", StringComparison.OrdinalIgnoreCase)
            || !uri.Host.EndsWith("strava.com", StringComparison.OrdinalIgnoreCase),
        "Bitte bei Strava anmelden (am besten per E-Mail – Google lässt die Anmeldung in eingebetteten Fenstern oft nicht zu). „Eingeloggt bleiben“ ist vorausgewählt – so geht es beim nächsten Mal direkt zur Upload-Seite.",
        "Upload-Seite", "Strava lädt die Datei hoch – danach Titel prüfen und „Speichern & anzeigen“ klicken.");

    /// <summary>Own browser profile (cookies) per platform, apart from the help window's, so signing out can simply delete it.</summary>
    public string ProfileFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErgoWattControl", "WebView2-" + ProfileName);

    /// <summary>Written once the platform itself (not its sign-in) was reached; removed when it asks to sign in again.</summary>
    public string SessionMarker => Path.Combine(ProfileFolder, "angemeldet.txt");

    /// <summary>Signed in before with a session that was still valid last time (it may have expired since).</summary>
    public bool HasSession => File.Exists(SessionMarker);
}

/// <summary>
/// An upload platform (Garmin Connect, Strava) in a window of its own (WebView2) to upload exported FIT files. The
/// rider signs in there himself – the app never sees or stores the password; the window doesn't offer to save it
/// either. The platform's session cookie stays in the window's own profile folder, so the next time it opens the
/// upload page directly. There the exported files are put into the page's file field; the strip above the page
/// also lets them be dragged in by hand.
/// </summary>
public sealed class UploadWindow : Form
{
    private static readonly Dictionary<UploadService, UploadWindow> OpenWindows = [];

    private readonly UploadService _service;
    private readonly AppSettings _settings;
    private readonly WebView2 _view = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(22, 22, 22) };
    private readonly FlowLayoutPanel _fileStrip = new() { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
    private readonly System.Windows.Forms.Timer _insertTimer = new() { Interval = 1000 };
    private List<string> _files = [];
    private bool _uploadOpened;
    private bool _inserted;
    private int _insertTries;

    private string UploadUrl => _service.UploadUrl(_settings);

    /// <summary>
    /// The platform's window with these freshly exported FIT files, modal over the export dialog: back there once it
    /// is closed (the next platform, or more exports).
    /// </summary>
    public static void Upload(UploadService service, AppSettings settings, IReadOnlyList<string> fitFiles, IWin32Window owner)
    {
        using var window = new UploadWindow(service, settings) { ShowInTaskbar = false };
        OpenWindows[service] = window;
        window.SetFiles(fitFiles);
        window.ShowDialog(owner);
    }

    /// <summary>
    /// "Anmelden" in the settings: the window without files, on the sign-in page, modal over the settings – it is
    /// only about signing in and closes by itself afterwards. Returns whether a sign-in is saved now.
    /// </summary>
    public static bool SignIn(UploadService service, AppSettings settings, IWin32Window owner)
    {
        using var window = new UploadWindow(service, settings, signIn: true) { ShowInTaskbar = false };
        OpenWindows[service] = window;
        window.ShowDialog(owner);
        return service.HasSession;
    }

    /// <summary>
    /// Whether the saved sign-in still works: opens the upload page in an invisible browser of the platform's profile
    /// and watches whether the platform sends it to its sign-in. True = still signed in, false = expired (the saved
    /// marker is removed), null = could not be checked (no WebView2, no network, window open, timeout).
    /// </summary>
    public static async Task<bool?> CheckSessionAsync(UploadService service, AppSettings settings, IntPtr hostWindow)
    {
        if (!service.HasSession || (OpenWindows.TryGetValue(service, out var open) && !open.IsDisposed))
            return null;
        CoreWebView2Controller? controller = null;
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(null, service.ProfileFolder);
            controller = await environment.CreateCoreWebView2ControllerAsync(hostWindow);
            controller.IsVisible = false;
            var core = controller.CoreWebView2;
            // Where the page ends up counts, not what it passes: a platform may pass its sign-in and come straight
            // back when that still knows the rider (Garmin's single sign-on).
            var navigating = 0;
            var lastOk = true;
            var completed = new TaskCompletionSource();
            core.NavigationStarting += (_, _) => navigating++;
            core.NavigationCompleted += (_, e) =>
            {
                navigating = Math.Max(0, navigating - 1);
                lastOk = e.IsSuccess;
                completed.TrySetResult();
            };
            core.Navigate(service.UploadUrl(settings));
            if (await Task.WhenAny(completed.Task, Task.Delay(20000)) != completed.Task)
                return null;
            // Settle: same address for 3 s without a navigation running (script redirects come after loading).
            var source = core.Source;
            for (int stable = 0, waited = 0; stable < 3 && waited < 12; waited++)
            {
                await Task.Delay(1000);
                stable = navigating == 0 && core.Source == source ? stable + 1 : 0;
                source = core.Source;
            }
            if (!lastOk || !Uri.TryCreate(source, UriKind.Absolute, out var final))
                return null;
            if (service.IsLoginPage(final))
            {
                TryDelete(service.SessionMarker);
                return false;
            }
            return Uri.TryCreate(service.UploadUrl(settings), UriKind.Absolute, out var upload)
                && final.Host.Equals(upload.Host, StringComparison.OrdinalIgnoreCase) ? true : null;
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or InvalidOperationException or System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
        finally
        {
            controller?.Close();
        }
    }

    /// <summary>Opened by "Anmelden" in the settings: sign in and close, no upload page, no file strip.</summary>
    private bool _signIn;
    private Control[] _fileControls = [];

    /// <summary>Deletes the saved sign-in. Not possible while the window is open (its browser holds the files).</summary>
    public static bool SignOut(UploadService service)
    {
        if (OpenWindows.TryGetValue(service, out var open) && !open.IsDisposed)
            return false;
        try
        {
            if (Directory.Exists(service.ProfileFolder))
                Directory.Delete(service.ProfileFolder, recursive: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private UploadWindow(UploadService service, AppSettings settings, bool signIn = false)
    {
        _service = service;
        _settings = settings;
        _signIn = signIn;
        Text = signIn ? $"{service.Name} – Anmelden" : $"{service.Name} – Aufzeichnung hochladen";
        Icon = AppIcon.Value;
        // Most of the main monitor (never the rider monitor): the platforms' pages need the room. With two
        // windows open the second one is offset a little so both title bars stay visible.
        StartPosition = FormStartPosition.Manual;
        var area = Screen.PrimaryScreen!.WorkingArea;
        var size = new Size(area.Width * 92 / 100, area.Height * 92 / 100);
        var offset = OpenWindows.Values.Count(w => !w.IsDisposed) * 30;
        Bounds = new Rectangle(area.X + (area.Width - size.Width) / 2 + offset, area.Y + (area.Height - size.Height) / 2 + offset,
            size.Width - offset, size.Height - offset);
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(32, 32, 32);

        var caption = new Label
        {
            Dock = DockStyle.Top, AutoSize = false, Height = 26, Padding = new Padding(8, 6, 0, 0),
            Text = $"Exportierte Dateien – werden auf der {service.PageName} eingesetzt, lassen sich aber auch hineinziehen:",
        };
        var insert = new Button { Text = "Erneut einsetzen", AutoSize = true, Margin = new Padding(8, 4, 4, 4) };
        insert.Click += (_, _) => StartInsert();
        var openFolder = new Button { Text = "Ordner öffnen", AutoSize = true, Margin = new Padding(4) };
        openFolder.Click += (_, _) =>
        {
            if (_files.Count > 0 && Path.GetDirectoryName(_files[0]) is { } folder)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
        };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false };
        actions.Controls.AddRange([insert, openFolder]);
        var strip = new Panel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(4, 0, 4, 0) };
        strip.Controls.Add(_fileStrip);
        strip.Controls.Add(actions);

        Controls.Add(_view);
        Controls.Add(strip);
        Controls.Add(caption);
        Controls.Add(_status);
        // Signing in only (from the settings): the page and the status line, no file strip.
        _fileControls = [caption, strip];
        foreach (var control in _fileControls)
            control.Visible = !signIn;

        _insertTimer.Tick += (_, _) => TryInsert();
        FormClosed += (_, _) =>
        {
            _insertTimer.Dispose();
            _rememberTimer?.Dispose();
            OpenWindows.Remove(_service);
        };
        Load += async (_, _) => await StartBrowser();
    }

    /// <summary>
    /// Ticks the sign-in page's "keep me signed in" box (Garmin: „Daten speichern“, Strava: „Eingeloggt bleiben“)
    /// so the next upload goes straight to the upload page. Only the checkbox – the rider still types e-mail and
    /// password himself. Once per window, so unticking it by hand sticks. The page builds its form after loading,
    /// so this retries for a few seconds.
    /// </summary>
    private void TickRemember()
    {
        if (_rememberDone || _rememberTimer is not null || _view.CoreWebView2 is null)
            return;
        const string script =
            "(() => { const re = /daten speichern|eingeloggt bleiben|angemeldet bleiben|remember|keep me/i;" +
            " for (const box of document.querySelectorAll('input[type=checkbox]')) {" +
            "  const text = [box.id, box.name, box.getAttribute('aria-label'), box.labels && box.labels[0] && box.labels[0].innerText," +
            "   box.closest('label') && box.closest('label').innerText, box.parentElement && box.parentElement.innerText].join(' ');" +
            "  if (re.test(text)) { if (!box.checked) box.click(); return box.checked ? 'ok' : 'nein'; } }" +
            " return 'none'; })()";
        var tries = 0;
        _rememberTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _rememberTimer.Tick += async (_, _) =>
        {
            _rememberTimer?.Stop();
            string result;
            try { result = _view.CoreWebView2 is { } core ? await core.ExecuteScriptAsync(script) : "\"none\""; }
            catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { result = "\"none\""; }
            if (result.Contains("ok") || ++tries > 20 || IsDisposed)
            {
                _rememberDone = result.Contains("ok");
                _rememberTimer?.Dispose();
                _rememberTimer = null;
                return;
            }
            _rememberTimer?.Start();
        };
        _rememberTimer.Start();
    }

    private bool _rememberDone;
    private System.Windows.Forms.Timer? _rememberTimer;
    private bool _closing;

    private void SetFiles(IReadOnlyList<string> files)
    {
        _files = [.. files];
        _fileStrip.Controls.Clear();
        foreach (var file in _files)
        {
            var chip = new Label
            {
                Text = Path.GetFileName(file),
                AutoSize = true,
                Padding = new Padding(8, 5, 8, 5),
                Margin = new Padding(4, 8, 4, 4),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
                BackColor = Color.FromArgb(45, 45, 45),
            };
            // Drag the file like from Explorer onto the page's drop area.
            chip.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left && File.Exists(file))
                    chip.DoDragDrop(new DataObject(DataFormats.FileDrop, new[] { file }), DragDropEffects.Copy);
            };
            _fileStrip.Controls.Add(chip);
        }
        _inserted = false;
        // Window already open (new export): back to the upload page, or straight in when it is showing.
        if (_view.CoreWebView2 is { } core)
        {
            if (core.Source.StartsWith(UploadUrl, StringComparison.OrdinalIgnoreCase))
                StartInsert();
            else
                core.Navigate(UploadUrl);
        }
    }

    private async Task StartBrowser()
    {
        try
        {
            Directory.CreateDirectory(_service.ProfileFolder);
            var environment = await CoreWebView2Environment.CreateAsync(null, _service.ProfileFolder);
            await _view.EnsureCoreWebView2Async(environment);
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or InvalidOperationException or System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException)
        {
            // Without WebView2: the normal browser at least gets the rider to the page.
            _status.Text = $"Eingebettetes Browserfenster nicht verfügbar ({ex.Message}) – {_service.Name} wird im Standardbrowser geöffnet.";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(UploadUrl) { UseShellExecute = true });
            return;
        }
        var core = _view.CoreWebView2;
        // The password stays with the rider (and the platform): no "save password?" offer in this window.
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.NewWindowRequested += (_, e) =>
        {
            // Links meant for a new tab stay in this window.
            e.Handled = true;
            core.Navigate(e.Uri);
        };
        core.SourceChanged += (_, _) => OnPage(core.Source);
        core.NavigationCompleted += (_, _) => OnPage(core.Source);
        core.Navigate(_service.HasSession && !_signIn ? UploadUrl : _service.LoginUrl(_settings));
    }

    /// <summary>Follows where the platform leads: sign-in, its start page after signing in, the upload page.</summary>
    private void OnPage(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            return;
        if (_service.IsLoginPage(uri))
        {
            TryDelete(_service.SessionMarker);
            _status.Text = _service.LoginHint;
            TickRemember();
            return;
        }
        if (!Uri.TryCreate(UploadUrl, UriKind.Absolute, out var upload) || !uri.Host.Equals(upload.Host, StringComparison.OrdinalIgnoreCase))
            return;
        try { File.WriteAllText(_service.SessionMarker, DateTime.Now.ToString("O")); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        // Signing in from the settings: done once the platform itself is reached – no upload page, the window closes.
        if (_signIn)
        {
            if (_closing)
                return;
            _closing = true;
            _status.Text = $"✓ Bei {_service.Name} angemeldet – das Fenster schließt sich.";
            var close = new System.Windows.Forms.Timer { Interval = 2500 };
            close.Tick += (_, _) =>
            {
                close.Dispose();
                if (_signIn && !IsDisposed)
                    Close();
            };
            close.Start();
            return;
        }
        if (url.StartsWith(UploadUrl, StringComparison.OrdinalIgnoreCase))
        {
            if (!_inserted && !_insertTimer.Enabled)
                StartInsert();
            return;
        }
        // Signed in: the platform lands on its start page – on to the upload page, once (the rider may browse on afterwards).
        if (!_uploadOpened)
        {
            _uploadOpened = true;
            _view.CoreWebView2.Navigate(UploadUrl);
        }
    }

    private void StartInsert()
    {
        if (_files.Count == 0)
            return;
        _inserted = false;
        _insertTries = 0;
        _status.Text = $"Suche das Dateifeld der {_service.PageName} …";
        _insertTimer.Start();
    }

    /// <summary>
    /// Puts the files into the page's file field via the DevTools protocol, which fires the same change event as
    /// choosing them in the file dialog. The page builds its form after loading, so this retries for a while.
    /// </summary>
    private async void TryInsert()
    {
        if (_inserted || _view.CoreWebView2 is not { } core || IsDisposed)
        {
            _insertTimer.Stop();
            return;
        }
        if (++_insertTries > 20)
        {
            _insertTimer.Stop();
            _status.Text = "Dateifeld nicht gefunden – bitte die Datei oben auf die Seite ziehen oder dort die Datei auswählen.";
            return;
        }
        _insertTimer.Stop();
        try
        {
            const string findInput =
                "(() => { const find = r => { const i = r.querySelector('input[type=file]'); if (i) return i;" +
                " for (const e of r.querySelectorAll('*')) if (e.shadowRoot) { const f = find(e.shadowRoot); if (f) return f; } return null; };" +
                " return find(document); })()";
            var found = await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate",
                JsonSerializer.Serialize(new { expression = findInput, returnByValue = false }));
            using var doc = JsonDocument.Parse(found);
            if (!doc.RootElement.GetProperty("result").TryGetProperty("objectId", out var objectId))
            {
                _insertTimer.Start();
                return;
            }
            var existing = _files.Where(File.Exists).ToArray();
            try
            {
                await core.CallDevToolsProtocolMethodAsync("DOM.setFileInputFiles",
                    JsonSerializer.Serialize(new { files = existing, objectId = objectId.GetString() }));
            }
            catch (Exception) when (existing.Length > 1)
            {
                // A field for a single file: at least the first one.
                await core.CallDevToolsProtocolMethodAsync("DOM.setFileInputFiles",
                    JsonSerializer.Serialize(new { files = existing.Take(1).ToArray(), objectId = objectId.GetString() }));
                existing = existing.Take(1).ToArray();
            }
            _inserted = true;
            _status.Text = (existing.Length == 1 ? $"✓ {Path.GetFileName(existing[0])} eingesetzt – " : $"✓ {existing.Length} Dateien eingesetzt – ")
                + _service.NextStep;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or ArgumentException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            _insertTimer.Start();
        }
    }

    private static void TryDelete(string file)
    {
        try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
