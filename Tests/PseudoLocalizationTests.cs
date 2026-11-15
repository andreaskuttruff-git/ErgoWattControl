using System.Reflection;
using System.Text.RegularExpressions;

namespace ErgoWattControl.Tests;

/// <summary>
/// Pseudo-localization (<see cref="L.Pseudo"/>): every window is built with all texts accented, ~40 % longer and between
/// ⟦ ⟧. Then a text without brackets was set past the resources (hard-coded), a text that doesn't fit its fixed field
/// would be cut off in a wordier language, and a dialog must still fit the rider's monitor.
/// </summary>
public partial class PseudoLocalizationTests
{
    /// <summary>The ride view's monitor at the ergometer; no window may grow beyond it.</summary>
    private static readonly Size RideMonitor = new(1280, 1024);

    /// <summary>Texts that are the same in every language: product names, units, the languages' own names.</summary>
    private static readonly HashSet<string> SameInEveryLanguage =
        ["ErgoWattControl", "RPM", "km/h", "W", "kg", "%", "m²", "Garmin Connect", "Strava", "Deutsch", "English", "Pseudo (test)", "Ergo Watt Control"];

    [GeneratedRegex(@"\p{L}")]
    private static partial Regex Letter();

    [GeneratedRegex(@"^COM\d+$")]
    private static partial Regex ComPort();

    /// <summary>A measured value with its unit ("100 W", "0,0 km", "0 kJ") or a file extension (".json"): no words.</summary>
    [GeneratedRegex(@"^(-?[\d.,:]+\s*(W|km|kJ|km/h|m|%)|\.[a-z]{2,4})$")]
    private static partial Regex ValueOrExtension();

    // ---------- the pseudo texts themselves

    [Theory]
    [InlineData("Settings", "⟦Šéţţîñĝš")]
    [InlineData("Testing {0} …", "{0}")]
    [InlineData("{0:0.0} of {1:0.0} km", "{1:0.0}")]
    public void PseudoText_IsAccentedLongerAndKeepsPlaceholders(string text, string contained)
    {
        var pseudo = L.PseudoText("Key", text);
        Assert.Contains(contained, pseudo);
        Assert.StartsWith(L.PseudoStart.ToString(), pseudo);
        Assert.EndsWith(L.PseudoEnd.ToString(), pseudo);
        // Longer by about 40 % of the letters (placeholders don't count: they are filled with values).
        var letters = text.Count(char.IsLetter);
        Assert.True(pseudo.Count(c => c == '·') >= Math.Max(2, letters * 2 / 5), $"{pseudo} ist kaum länger als {text}");
    }

    [Fact]
    public void PseudoText_KeepsAddressesAndFileFilters()
    {
        Assert.Equal("https://example.org/Help.html", L.PseudoText("Info_HelpUrl", "https://example.org/Help.html"));
        var filter = L.PseudoText("Route_Filter", "Routes (*.gpx, *.fit)|*.gpx;*.fit");
        Assert.EndsWith("|*.gpx;*.fit", filter);
        Assert.StartsWith(L.PseudoStart.ToString(), filter);
    }

    [Fact]
    public void EveryText_CanBeFormattedInPseudo()
    {
        // Every text of the resources, pseudo-localized, must still be a valid format with the same placeholders.
        WithPseudo(() =>
        {
            var keys = typeof(L).Assembly.GetManifestResourceNames().Length > 0
                ? new System.Resources.ResourceManager("ErgoWattControl.Localization.Strings", typeof(L).Assembly)
                    .GetResourceSet(System.Globalization.CultureInfo.InvariantCulture, true, true)!
                    .Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Key).ToList()
                : [];
            Assert.True(keys.Count > 300, $"nur {keys.Count} Texte gefunden");
            foreach (var key in keys)
                L.F(key, new object[10]); // throws FormatException if the pseudo form broke a placeholder
            return 0;
        });
    }

    [Fact]
    public void LanguageSetting_Pseudo_SwitchesItOn_AndOtherLanguagesOff()
    {
        try
        {
            Language.Apply(Language.PseudoCode);
            Assert.True(L.Pseudo);
            Assert.StartsWith(L.PseudoStart.ToString(), L.T("Settings_Title"));
            Language.Apply("de");
            Assert.False(L.Pseudo);
            Assert.Equal("Einstellungen", L.T("Settings_Title"));
        }
        finally
        {
            Language.Apply("de");
        }
    }

    // ---------- every window, built in pseudo

    /// <summary>The windows to check, each built fresh in pseudo-localization.</summary>
    public static TheoryData<string> Windows => new(WindowFactories.Keys);

    private static readonly Dictionary<string, Func<Form>> WindowFactories = new()
    {
        ["Fahransicht"] = () => new RideForm(Settings(), demo: true, startOnMainScreen: true),
        ["Einstellungen"] = () => new SettingsForm(Settings(), new ErgoController()),
        ["Export"] = () => new ExportForm(SettingsWithRecording()),
        ["Programme"] = () => { ProgramFiles.Install(); return ReplayPickerForm.ForPrograms(ProgramFiles.Folder, ProgramFiles.CustomFolder, Settings(), _ => { }); },
        ["Aufzeichnungen"] = () => ReplayPickerForm.ForRecordings(SettingsWithRecording(), _ => { }),
        ["Strecken"] = () => new RoutePickerForm(Settings(), 800),
        ["Beenden (App)"] = () => new RenameRecordingForm(Path.Combine(TestFolder.Path, "Lauf.json"), closingApp: true),
        ["Beenden (Training)"] = () => new RenameRecordingForm(Path.Combine(TestFolder.Path, "Lauf.json"), endTraining: true),
        ["Zusammenfassung"] = () => new SummaryForm(L.T("End_Ask"), L.F("End_FinishHeading", "Tour"),
            [(L.T("Fact_Route"), L.F("Fact_RouteValue", 12.3, 40.0)), (L.T("Fact_AvgPower"), "150 W")], Path.Combine(TestFolder.Path, "Lauf.json"), canResume: true),
        ["Info"] = () => new InfoForm(),
    };

    private static AppSettings Settings() => new()
    {
        RecordingDirectory = TestFolder.New("pseudo"),
        GpxDirectory = TestFolder.New("pseudo_gpx"),
    };

    private static AppSettings SettingsWithRecording()
    {
        var settings = Settings();
        var file = new RecordingFile { Start = DateTime.Now, Settings = new AppSettings(), Columns = RecordingFile.BaseColumns };
        for (var s = 0; s < 30; s++)
            file.Values.Add([s, s, 100, 100, 80, 120, 25, s / 100.0, s / 10.0, s]);
        file.Save(Path.Combine(settings.RecordingDirectory, "Training_2026-11-15_10-00-00.json"));
        return settings;
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void Window_HasNoHardCodedText(string window)
    {
        var problems = InPseudoWindow(window, HardCoded);
        Assert.True(problems.Count == 0, "Texte ohne Übersetzung:\n" + string.Join("\n", problems.Distinct()));
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void Window_TextsFitTheirFields(string window)
    {
        var (problems, checkedFields) = InPseudoWindow(window, form => (Clipped(form, out var count), count));
        Assert.True(problems.Count == 0, "Abgeschnittene Texte:\n" + string.Join("\n", problems));
        Assert.True(checkedFields > 0, $"{window}: kein sichtbares Textfeld geprüft – zeigt das Fenster überhaupt etwas?");
    }

    // Counter-checks: the checks above really find what they look for.

    [Fact]
    public void Check_FindsAHardCodedText()
    {
        var problems = InPseudoForm(() =>
        {
            var form = new Form { Text = L.T("Settings_Title") };
            form.Controls.Add(new Label { Text = "Hallo", AutoSize = true });
            form.Controls.Add(new Label { Text = L.T("Settings_Device"), AutoSize = true, Top = 30 });
            return form;
        }, HardCoded);
        Assert.Equal(["Label : „Hallo“"], problems);
    }

    [Fact]
    public void Check_FindsATooNarrowField()
    {
        var problems = InPseudoForm(() =>
        {
            var form = new Form();
            form.Controls.Add(new Label { Name = "eng", Text = L.T("Settings_Title"), AutoSize = false, Width = 40 });
            form.Controls.Add(new Label { Name = "weit", Text = L.T("Settings_Title"), AutoSize = true, Top = 30 });
            return form;
        }, form => Clipped(form, out _));
        Assert.Single(problems);
        Assert.Contains("eng", problems[0]);
    }

    /// <summary>Visible texts of the interface without the pseudo brackets: set past the resources.</summary>
    private static List<string> HardCoded(Form form)
    {
        var found = new List<string>();
        void Check(string where, string? text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Contains(L.PseudoStart) || !Letter().IsMatch(text)
                || SameInEveryLanguage.Contains(text.Trim()) || ComPort().IsMatch(text.Trim()) || ValueOrExtension().IsMatch(text.Trim()))
                return;
            found.Add($"{where}: „{text}“");
        }
        Check("Titel", form.Text);
        var tips = ToolTips(form).ToList();
        foreach (var control in All(form).Where(c => c.Visible))
        {
            switch (control)
            {
                case TextBoxBase or NumericUpDown or ListBox or DataGridView or PictureBox:
                    break; // values and data, not texts of the interface
                case ComboBox combo:
                    foreach (var item in combo.Items)
                        Check($"{combo.Name} (Liste)", item?.ToString());
                    break;
                default:
                    Check($"{control.GetType().Name} {control.Name}", control.Text);
                    break;
            }
            foreach (var tip in tips)
                Check($"Tooltip von {control.GetType().Name} {control.Name}", tip.GetToolTip(control));
        }
        foreach (var item in MenuItems(form))
            Check("Menü", item.Text);
        return found;
    }

    /// <summary>Visible fixed-width labels and buttons whose (longer, pseudo) text doesn't fit on their line.</summary>
    private static List<string> Clipped(Form form, out int checkedFields)
    {
        var found = new List<string>();
        checkedFields = 0;
        foreach (var control in All(form).Where(c => c.Visible && c is Label or ButtonBase && !string.IsNullOrEmpty(c.Text)))
        {
            checkedFields++;
            // Only fixed-size fields can cut a text off; growing ones (AutoSize), ones that end in "…" on purpose and
            // multi-line ones (they wrap) are fine.
            if (control.AutoSize || control is Label { AutoEllipsis: true } || control is ButtonBase { AutoEllipsis: true }
                || control.Text.Contains('\n'))
                continue;
            var needed = TextRenderer.MeasureText(control.Text, control.Font, Size.Empty, TextFormatFlags.SingleLine);
            var room = control.ClientSize.Width - control.Padding.Horizontal - (control is ButtonBase ? 6 : 0);
            if (needed.Width > room + 1)
                found.Add($"{control.GetType().Name} {control.Name} „{control.Text}“: braucht {needed.Width} px, hat {room} px");
        }
        return found;
    }

    /// <summary>The dialogs; the ride view itself is sized to the monitor by the app, so it can't grow beyond it.</summary>
    public static TheoryData<string> Dialogs => new(WindowFactories.Keys.Where(w => w != "Fahransicht"));

    [Theory]
    [MemberData(nameof(Dialogs))]
    public void Window_FitsTheRideMonitor(string window)
    {
        var size = InPseudoWindow(window, form => form.Size);
        Assert.True(size.Width <= RideMonitor.Width && size.Height <= RideMonitor.Height,
            $"{window} ist mit längeren Texten {size.Width} × {size.Height} px groß, der Monitor am Ergometer {RideMonitor.Width} × {RideMonitor.Height}.");
    }

    // ---------- helpers

    private static T WithPseudo<T>(Func<T> action)
    {
        try
        {
            Language.Apply(Language.PseudoCode);
            return action();
        }
        finally
        {
            Language.Apply("de");
        }
    }

    /// <summary>Builds the window in pseudo-localization and inspects it (see <see cref="InPseudoForm{T}"/>).</summary>
    private static T InPseudoWindow<T>(string window, Func<Form, T> inspect) =>
        InPseudoForm(() =>
        {
            var form = WindowFactories[window]();
            if (form is RideForm)
                form.Bounds = new Rectangle(Point.Empty, RideMonitor);
            return form;
        }, inspect, window);

    /// <summary>
    /// Builds a window on an STA thread (as WinForms wants it) with the texts in pseudo-localization, shows it far off
    /// the screen so its controls are really visible and laid out, and inspects it. No message loop runs: events that
    /// come later (Shown – e.g. connecting the device or checking a saved sign-in) never fire.
    /// </summary>
    private static T InPseudoForm<T>(Func<Form> create, Func<Form, T> inspect, string what = "Fenster")
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = WithPseudo(() =>
                {
                    using var form = create();
                    form.StartPosition = FormStartPosition.Manual;
                    form.ShowInTaskbar = false;
                    form.Location = new Point(-20000, -20000);
                    form.Show();
                    form.PerformLayout();
                    var value = inspect(form);
                    form.Hide();
                    return value;
                });
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
            throw new InvalidOperationException($"{what}: {error.Message}", error);
        return result;
    }

    private static IEnumerable<Control> All(Control parent) =>
        parent.Controls.Cast<Control>().SelectMany(c => All(c).Prepend(c));

    /// <summary>The window's tooltips (fields of type ToolTip, also inherited ones).</summary>
    private static IEnumerable<ToolTip> ToolTips(Form form) =>
        form.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.FieldType == typeof(ToolTip))
            .Select(f => f.GetValue(form)).OfType<ToolTip>();

    /// <summary>Menu entries reachable from the window's menu item fields (the ride view's ☰ menu).</summary>
    private static IEnumerable<ToolStripItem> MenuItems(Form form) =>
        form.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(f => typeof(ToolStripItem).IsAssignableFrom(f.FieldType))
            .Select(f => f.GetValue(form)).OfType<ToolStripItem>()
            .SelectMany(i => i.Owner?.Items.Cast<ToolStripItem>() ?? [i])
            .Where(i => i is not ToolStripSeparator)
            .Distinct();
}
