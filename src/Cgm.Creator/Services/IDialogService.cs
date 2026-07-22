namespace Cgm.Creator.Services;

public sealed record NewProjectRequest(string Folder, string Name, int TileSize);

/// <summary>The unsaved-changes guard's three outcomes (CREATOR_APP_SPEC §10.5).</summary>
public enum UnsavedChoice { Save, Discard, Cancel }

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

    /// <summary>The §10.5 unsaved guard: Save / Discard / Cancel. Closing the dialog is Cancel.</summary>
    Task<UnsavedChoice> PromptUnsavedAsync();

    /// <summary>A yes/no confirmation; closing the dialog is no.</summary>
    Task<bool> ConfirmAsync(string message);
}
