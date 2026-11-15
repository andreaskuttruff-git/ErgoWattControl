using System.Globalization;
using System.Resources;
using System.Text;

namespace ErgoWattControl;

/// <summary>
/// The texts of the user interface, from Localization\Strings.resx (English, also for languages without a translation)
/// and Strings.&lt;language&gt;.resx (e.g. Strings.de.resx). <c>L.T(key)</c> gives a text, <c>L.F(key, args)</c> fills its
/// placeholders {0}, {1} … with numbers formatted as Windows is set (decimal comma or point). A unit test checks that
/// every key used in the code exists in every language with the same placeholders.
/// </summary>
public static class L
{
    private static readonly ResourceManager Resources = new("ErgoWattControl.Localization.Strings", typeof(L).Assembly);

    /// <summary>
    /// Pseudo-localization ("Language": "pseudo" in settings.json, and the tests): every text comes accented, about
    /// 40 % longer and between ⟦ ⟧ – so a text set past the resources (no brackets), a text cut off by a too narrow
    /// field, or a window that grows too large shows at once.
    /// </summary>
    public static bool Pseudo { get; set; }

    public const char PseudoStart = '⟦';
    public const char PseudoEnd = '⟧';

    /// <summary>The text for <paramref name="key"/> in the language of the user interface; "[key]" if it is missing.</summary>
    public static string T(string key)
    {
        var text = Resources.GetString(key, CultureInfo.CurrentUICulture) ?? $"[{key}]";
        return Pseudo ? PseudoText(key, text) : text;
    }

    /// <summary>The text for <paramref name="key"/> with its placeholders filled.</summary>
    public static string F(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);

    /// <summary>The text in a given language (tests, language list).</summary>
    public static string T(string key, CultureInfo language) => Resources.GetString(key, language) ?? $"[{key}]";

    private const string Plain = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Accented = "àƀçđéƒĝĥîĵķľɱñöþǫŕšţûṽŵẋýžÀßÇĐÉƑĜĤÎĴĶĽṀÑÖÞǪŔŠŢÛṼŴẊÝŽ";

    /// <summary>
    /// The pseudo form of a text: letters accented, placeholders ({0}, {1:0.0}) and line breaks kept, about 40 % longer,
    /// between ⟦ ⟧. Texts that are not prose stay usable: web addresses unchanged, and in a file filter
    /// ("Description|*.json") only the descriptions change.
    /// </summary>
    public static string PseudoText(string key, string text)
    {
        if (key.EndsWith("Url", StringComparison.Ordinal))
            return text;
        if (text.Contains('|'))
        {
            var parts = text.Split('|');
            for (var i = 0; i < parts.Length; i += 2)
                parts[i] = PseudoText("", parts[i]);
            return string.Join('|', parts);
        }
        var result = new StringBuilder(text.Length * 2);
        result.Append(PseudoStart);
        var letters = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '{')
            {
                // A placeholder (or an escaped "{{") is copied as it is.
                var end = text.IndexOf('}', i);
                if (end < 0)
                    end = text.Length - 1;
                result.Append(text, i, end - i + 1);
                i = end;
                continue;
            }
            var plain = Plain.IndexOf(c);
            result.Append(plain >= 0 ? Accented[plain] : c);
            if (char.IsLetter(c))
                letters++;
        }
        // Longer like a translation into a wordier language (German, Finnish …): about 40 %, at least two characters.
        result.Append(' ').Append('·', Math.Max(2, letters * 2 / 5));
        result.Append(PseudoEnd);
        return result.ToString();
    }
}

/// <summary>
/// The language of the user interface: chosen in the settings ("auto" = as Windows, "de", "en"), applied once at start
/// (a change takes effect after a restart). Numbers and dates keep following the Windows format settings.
/// "pseudo" (only by editing settings.json) shows the English texts pseudo-localized, see <see cref="L.Pseudo"/>.
/// </summary>
public static class Language
{
    public const string Auto = "auto";
    public const string PseudoCode = "pseudo";

    /// <summary>
    /// The display language of Windows (the user's, which a language pack may set apart from the language Windows was
    /// installed in), taken before <see cref="Apply"/> first changes the app's: the class is set up on that first call.
    /// </summary>
    private static readonly CultureInfo Windows = CultureInfo.CurrentUICulture;

    /// <summary>The languages the app has texts for; a new one is a Strings.&lt;code&gt;.resx plus an entry here.</summary>
    public static readonly IReadOnlyList<(string Code, string Name)> Available =
    [
        ("de", "Deutsch"),
        ("en", "English"),
    ];

    /// <summary>The language a setting stands for: an available one, or for "auto"/unknown the Windows display language if available, else English.</summary>
    public static CultureInfo Resolve(string? setting)
    {
        var code = Available.Any(l => l.Code == setting) ? setting! : Windows.TwoLetterISOLanguageName;
        return CultureInfo.GetCultureInfo(Available.Any(l => l.Code == code) ? code : "en");
    }

    /// <summary>A setting as stored: "auto", an available language or "pseudo"; anything else (e.g. "fr", "DE", null) means "auto".</summary>
    public static string Normalize(string? setting)
    {
        var code = setting?.Trim().ToLowerInvariant();
        return code == PseudoCode || Available.Any(l => l.Code == code) ? code! : Auto;
    }

    /// <summary>Sets the language of the user interface for the whole app (all threads).</summary>
    public static void Apply(string? setting)
    {
        L.Pseudo = setting == PseudoCode;
        var culture = L.Pseudo ? CultureInfo.GetCultureInfo("en") : Resolve(setting);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
