using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Cgm.Creator.Views;

namespace Cgm.Creator.Services;

/// <summary>Avalonia implementation of the dialogs. New-project uses a folder pick (name defaults
/// to the folder name; rename later in project settings) to avoid a custom dialog window in Phase 3.</summary>
public sealed class AvaloniaDialogService : IDialogService
{
    private readonly Func<TopLevel?> _topLevel;

    public AvaloniaDialogService(Func<TopLevel?> topLevel) => _topLevel = topLevel;

    public async Task<string?> PickProjectFolderAsync()
    {
        string? path = await PickFolderAsync("Open Project");
        return path;
    }

    public async Task<NewProjectRequest?> PromptNewProjectAsync()
    {
        string? path = await PickFolderAsync("New Project — pick an empty folder");
        if (path is null) return null;
        string name = Path.GetFileName(path.TrimEnd('/', '\\'));
        return new NewProjectRequest(path, name.Length > 0 ? name : "New Project", 16);
    }

    public async Task<string?> PromptTextAsync(string prompt, string initial)
    {
        if (_topLevel() is not Window owner) return null;
        return await new PromptWindow(prompt, initial).ShowDialog<string?>(owner);
    }

    public async Task<string?> PickPngAsync()
    {
        if (_topLevel() is not { } top) return null;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import PNG sheet",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("PNG images") { Patterns = ["*.png"] }],
        });
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async Task<UnsavedChoice> PromptUnsavedAsync()
    {
        if (_topLevel() is not Window owner) return UnsavedChoice.Cancel;
        int? choice = await new ChoiceWindow("The project has unsaved changes.",
            "Discard", "Cancel", "Save").ShowDialog<int?>(owner);
        return choice switch { 0 => UnsavedChoice.Discard, 2 => UnsavedChoice.Save, _ => UnsavedChoice.Cancel };
    }

    public async Task<bool> ConfirmAsync(string message)
    {
        if (_topLevel() is not Window owner) return false;
        int? choice = await new ChoiceWindow(message, "No", "Yes").ShowDialog<int?>(owner);
        return choice == 1;
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        if (_topLevel() is not { } top) return null;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
}
