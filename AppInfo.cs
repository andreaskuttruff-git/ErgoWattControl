using System.Reflection;

namespace ErgoWattControl;

/// <summary>Version of this build and the project page, for the "Info" line in the settings.</summary>
public static class AppInfo
{
    public const string ProjectUrl = "https://github.com/andreaskuttruff-git/ErgoWattControl";

    /// <summary>
    /// "1.3.0 (89e4e48)": the version from the release tag (Setup\build-setup.ps1 -Version, otherwise &lt;Version&gt; in the
    /// csproj) and the short commit the build was made from, which the SDK appends as "+&lt;hash&gt;".
    /// </summary>
    public static string Version { get; } = Describe(
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "?");

    private static string Describe(string informational)
    {
        var plus = informational.IndexOf('+');
        if (plus < 0)
            return informational;
        var commit = informational[(plus + 1)..];
        return $"{informational[..plus]} ({commit[..Math.Min(7, commit.Length)]})";
    }
}
