using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErgoWattControl;

/// <summary>
/// A text in several languages, e.g. the name and description of a training program. In JSON either one plain string
/// (valid for every language – handy for one's own programs) or an object keyed by language code:
/// <c>{ "de": "Schwellenblöcke", "en": "Threshold blocks" }</c>. Another language later is just another key.
/// </summary>
[JsonConverter(typeof(LocalizedTextConverter))]
public sealed class LocalizedText
{
    /// <summary>Order in which languages are tried when the wanted one is missing.</summary>
    private static readonly string[] Fallbacks = ["en", "de"];

    private readonly Dictionary<string, string> _texts;

    /// <summary>The same text for every language.</summary>
    public LocalizedText(string text) => _texts = new() { [""] = text };

    public LocalizedText(IDictionary<string, string> texts) =>
        _texts = new(texts.Where(t => !string.IsNullOrWhiteSpace(t.Value)), StringComparer.OrdinalIgnoreCase);

    public static LocalizedText Empty => new("");

    /// <summary>One text for all languages (a plain string in JSON).</summary>
    public bool IsSingle => _texts.Count == 1 && _texts.ContainsKey("");

    /// <summary>The languages given explicitly (empty for a plain string).</summary>
    public IReadOnlyCollection<string> Languages => [.. _texts.Keys.Where(k => k.Length > 0)];

    public bool IsEmpty => _texts.Values.All(string.IsNullOrWhiteSpace);

    /// <summary>
    /// The text in <paramref name="language"/> (two-letter code), else English, else German, else any – so a program
    /// always shows something, even in a language nobody translated it into.
    /// </summary>
    public string In(string language)
    {
        foreach (var code in new[] { language, "" }.Concat(Fallbacks))
        {
            if (_texts.TryGetValue(code, out var text))
                return text;
        }
        return _texts.Values.FirstOrDefault() ?? "";
    }

    /// <summary>The text in the language of the user interface.</summary>
    public string Text => In(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public override string ToString() => Text;

    internal IReadOnlyDictionary<string, string> All => _texts;
}

/// <summary>Reads a plain string or a language object; writes a plain string only for a text without languages.</summary>
public sealed class LocalizedTextConverter : JsonConverter<LocalizedText>
{
    public override LocalizedText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return new LocalizedText(reader.GetString() ?? "");
            case JsonTokenType.Null:
                return LocalizedText.Empty;
            case JsonTokenType.StartObject:
                var texts = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options) ?? [];
                return new LocalizedText(texts);
            default:
                throw new JsonException("Erwartet: ein Text oder ein Objekt mit einem Text je Sprache, z. B. { \"de\": \"…\", \"en\": \"…\" }.");
        }
    }

    public override void Write(Utf8JsonWriter writer, LocalizedText value, JsonSerializerOptions options)
    {
        if (value.IsSingle)
        {
            writer.WriteStringValue(value.In(""));
            return;
        }
        writer.WriteStartObject();
        foreach (var (language, text) in value.All.OrderBy(t => t.Key == "de" ? 0 : t.Key == "en" ? 1 : 2).ThenBy(t => t.Key))
            writer.WriteString(language, text);
        writer.WriteEndObject();
    }
}
