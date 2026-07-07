namespace Cgm.Creator.Services;

public sealed record NewProjectRequest(string Folder, string Name, int TileSize);

/// <summary>
/// Abstracts the folder picker / new-project prompt so the shell view-model stays UI-free and
/// headlessly testable (a fake supplies canned paths in tests; Avalonia supplies real dialogs).
/// </summary>
public interface IDialogService
{
    Task<string?> PickProjectFolderAsync();
    Task<NewProjectRequest?> PromptNewProjectAsync();

    /// <summary>Prompts for a line of text (e.g. a new entity slug). Returns null if cancelled.</summary>
    Task<string?> PromptTextAsync(string prompt, string initial);

    /// <summary>Picks a PNG file to import. Returns null if cancelled.</summary>
    Task<string?> PickPngAsync();
}
