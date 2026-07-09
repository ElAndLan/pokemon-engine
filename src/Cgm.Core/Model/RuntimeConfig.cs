namespace Cgm.Core.Model;

/// <summary>
/// The <c>config.json</c> written beside the exported exe and read by the runtime at boot
/// (Addendum §6 / EXPORT_PIPELINE_SPEC). Pure data; the runtime fails fast if it is missing/invalid.
/// </summary>
public sealed record RuntimeConfig
{
    public int SchemaVersion { get; init; } = SchemaVersions.Current;
    public string GameName { get; init; } = "";
    public string WindowTitle { get; init; } = "";
    public int VirtualWidth { get; init; } = 240;
    public int VirtualHeight { get; init; } = 160;
    public string SaveDirName { get; init; } = "";
    public string PackPath { get; init; } = "game.cgmpack";
    public bool Debug { get; init; }

    /// <summary>Reduces a game name to a filesystem-safe folder name for <c>%APPDATA%/&lt;name&gt;</c>.
    /// Keeps letters/digits/space/underscore/dash, collapses runs of whitespace, trims; empty → "Game".</summary>
    public static string SafeSaveDir(string gameName)
    {
        var sb = new System.Text.StringBuilder(gameName.Length);
        bool lastSpace = false;
        foreach (char c in gameName.Trim())
        {
            if (char.IsLetterOrDigit(c) || c is '_' or '-')
            {
                sb.Append(c);
                lastSpace = false;
            }
            else if (char.IsWhiteSpace(c) && sb.Length > 0 && !lastSpace)
            {
                sb.Append(' ');
                lastSpace = true;
            }
        }
        string result = sb.ToString().Trim();
        return result.Length == 0 ? "Game" : result;
    }
}
