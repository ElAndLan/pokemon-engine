using Cgm.Creator.Services;

namespace Cgm.Creator.Tests;

internal sealed class FakeDialogService : IDialogService
{
    public string? FolderToReturn { get; set; }
    public NewProjectRequest? NewProjectToReturn { get; set; }
    public string? TextToReturn { get; set; }
    public string? PngToReturn { get; set; }
    public UnsavedChoice UnsavedChoiceToReturn { get; set; } = UnsavedChoice.Cancel;
    public bool ConfirmToReturn { get; set; }
    public int UnsavedPrompts { get; private set; }

    public Task<string?> PickProjectFolderAsync() => Task.FromResult(FolderToReturn);
    public Task<NewProjectRequest?> PromptNewProjectAsync() => Task.FromResult(NewProjectToReturn);
    public Task<string?> PromptTextAsync(string prompt, string initial) => Task.FromResult(TextToReturn);
    public Task<string?> PickPngAsync() => Task.FromResult(PngToReturn);

    public string? WavToReturn { get; set; }
    public Task<string?> PickWavAsync() => Task.FromResult(WavToReturn);

    public Task<UnsavedChoice> PromptUnsavedAsync()
    {
        UnsavedPrompts++;
        return Task.FromResult(UnsavedChoiceToReturn);
    }

    public Task<bool> ConfirmAsync(string message) => Task.FromResult(ConfirmToReturn);

    public Cgm.Core.Model.EntityId? EntityToReturn { get; set; }
    public IReadOnlyList<(Cgm.Core.Model.EntityId Id, string Name)>? LastPickCandidates { get; private set; }

    public Task<Cgm.Core.Model.EntityId?> PickEntityAsync(Cgm.Core.Model.EntityCategory category,
        IReadOnlyList<(Cgm.Core.Model.EntityId Id, string Name)> candidates, string prompt)
    {
        LastPickCandidates = candidates;
        return Task.FromResult(EntityToReturn);
    }
}
