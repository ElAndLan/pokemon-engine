using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation;
using Cgm.Creator.Services;

namespace Cgm.Creator.ViewModels;

/// <summary>The Creator shell (CREATOR_APP_SPEC §2): the open project session, navigation tree,
/// open document tabs, and the live validation strip. UI-free so it is headlessly testable — file
/// dialogs go through <see cref="IDialogService"/>; the core methods take plain paths.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly Editing.RecentProjects _recent;
    private readonly Editing.RecoverySnapshots _recovery;
    private DateTime _lastEditUtc;
    private bool _snapshotUpToDate;

    public MainWindowViewModel(IDialogService dialogs, Editing.RecentProjects? recent = null,
        Editing.RecoverySnapshots? recovery = null)
    {
        _dialogs = dialogs;
        _recent = recent ?? Editing.RecentProjects.Default();
        _recovery = recovery ?? Editing.RecoverySnapshots.Default();
        foreach (string folder in _recent.Folders)
            Recent.Add(folder);
    }

    [ObservableProperty] private ProjectSession? _session;
    [ObservableProperty] private EditorDocument? _activeDocument;
    [ObservableProperty] private string _statusText = "No project open.";
    [ObservableProperty] private string _projectName = "";

    public ObservableCollection<NavCategory> Nav { get; } = [];
    public ObservableCollection<EditorDocument> Documents { get; } = [];
    public ObservableCollection<ValidationIssue> Issues { get; } = [];
    public ObservableCollection<string> Recent { get; } = [];

    public bool HasProject => Session is not null;
    public int ErrorCount => Issues.Count(i => i.Severity == ValidationSeverity.Error);
    public int WarningCount => Issues.Count(i => i.Severity == ValidationSeverity.Warning);
    public string ValidationSummary => $"{ErrorCount} errors, {WarningCount} warnings";

    /// <summary>Entity categories the Creator can currently create from the UI (Phase 3).</summary>
    public IReadOnlyList<EntityCategory> CreatableCategories { get; } =
        [EntityCategory.Type, EntityCategory.Item, EntityCategory.Move, EntityCategory.Ability, EntityCategory.Species];

    [ObservableProperty] private EntityCategory _newCategory = EntityCategory.Move;

    // --- Commands (wire dialogs to the testable core) ---

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (!await ConfirmLoseChangesAsync()) return;
        if (await _dialogs.PickProjectFolderAsync() is { } folder && OpenProject(folder))
            await OfferRecoveryAsync();
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        if (!await ConfirmLoseChangesAsync()) return;
        if (await _dialogs.PromptNewProjectAsync() is { } request)
            NewProject(request);
    }

    /// <summary>Opens a recent entry; a missing folder offers removal from the list (§10.6).</summary>
    [RelayCommand]
    private async Task OpenRecentAsync(string folder)
    {
        if (!Directory.Exists(folder))
        {
            if (await _dialogs.ConfirmAsync($"'{folder}' no longer exists. Remove it from the recent list?"))
                RemoveRecent(folder);
            return;
        }
        if (!await ConfirmLoseChangesAsync()) return;
        if (OpenProject(folder))
            await OfferRecoveryAsync();
    }

    [RelayCommand]
    private void RemoveRecent(string folder)
    {
        _recent.Remove(folder);
        SyncRecent();
    }

    /// <summary>The §10.5 unsaved guard. True = proceed (clean, saved, or discarded); false =
    /// cancelled, or Save failed (never silently lose the edits behind a failed save). A true
    /// result is a clean close for the current project, so its recovery snapshots are discarded.</summary>
    public async Task<bool> ConfirmLoseChangesAsync()
    {
        bool proceed;
        if (Session is not { IsDirty: true })
        {
            proceed = true;
        }
        else
        {
            switch (await _dialogs.PromptUnsavedAsync())
            {
                case UnsavedChoice.Save:
                    SaveAll();
                    proceed = !Session.IsDirty; // a failed save leaves dirt and aborts the close
                    break;
                case UnsavedChoice.Discard:
                    proceed = true;
                    break;
                default:
                    proceed = false;
                    break;
            }
        }

        if (proceed && Session is { } session)
            _recovery.Discard(session.Folder);
        return proceed;
    }

    // --- Recovery snapshots (§10.4) ---

    /// <summary>Offers the newest recovery snapshot after an unclean previous session. Applying
    /// loads it as in-memory state, fully dirty; declining leaves snapshots on disk.</summary>
    public async Task OfferRecoveryAsync()
    {
        if (Session is null || _recovery.For(Session.Folder) is not [{ } newest, ..])
            return;

        string stamp = Path.GetFileName(newest);
        if (!await _dialogs.ConfirmAsync(
            $"The last session ended without a clean close. Apply the recovery snapshot from {stamp} (UTC)? " +
            "Project files stay untouched until you save."))
            return;

        Session.RestoreSnapshot(newest);
        Documents.Clear();
        ActiveDocument = null;
        RebuildNav();
        RefreshValidation();
        StatusText = $"Recovery snapshot {stamp} applied — save to keep it.";
    }

    /// <summary>Called by the shell timer. Snapshots after 120 s of dirty inactivity, once per
    /// edit burst; each edit re-arms it via <see cref="RefreshValidation"/>.</summary>
    public void AutosaveTick(DateTime nowUtc)
    {
        if (Session is not { IsDirty: true } session || _snapshotUpToDate
            || nowUtc - _lastEditUtc < TimeSpan.FromSeconds(120))
            return;
        _recovery.Write(session);
        _snapshotUpToDate = true;
    }

    /// <summary>App deactivation while dirty writes a snapshot immediately (§10.4).</summary>
    public void SnapshotNow()
    {
        if (Session is { IsDirty: true } session && !_snapshotUpToDate)
        {
            _recovery.Write(session);
            _snapshotUpToDate = true;
        }
    }

    [RelayCommand]
    private void Save() => SaveAll();

    [RelayCommand]
    private void Undo() => ActiveDocument?.Undo.Undo();

    [RelayCommand]
    private void Redo() => ActiveDocument?.Undo.Redo();

    [RelayCommand]
    private async Task NewEntityAsync()
    {
        if (Session is null) return;
        if (await _dialogs.PromptTextAsync($"New {NewCategory} — id slug (a-z, 0-9, _):", "") is { } slug)
            CreateEntity(NewCategory, slug);
    }

    [RelayCommand]
    private async Task DuplicateActiveAsync()
    {
        if (ActiveDocument?.Id is not { } id) return;
        if (await _dialogs.PromptTextAsync("Duplicate as — new id slug:", id.Slug + "_copy") is { } slug)
            DuplicateEntity(id, slug);
    }

    [RelayCommand]
    private void DeleteActive()
    {
        if (ActiveDocument?.Id is { } id)
            DeleteEntity(id);
    }

    [RelayCommand]
    private async Task ImportSheetAsync()
    {
        if (Session is null) return;
        if (await _dialogs.PickPngAsync() is not { } png) return;
        string suggested = Path.GetFileNameWithoutExtension(png).ToLowerInvariant();
        if (await _dialogs.PromptTextAsync("Sheet id slug:", suggested) is { } slug)
            ImportSheet(png, slug);
    }

    public void ImportSheet(string pngPath, string slug)
    {
        if (Session is null) return;
        if (!EntityId.IsValidSlug(slug))
        {
            StatusText = $"Invalid slug '{slug}'.";
            return;
        }
        var id = new EntityId(EntityCategory.Sheet, slug);
        if (Session.Contains(id))
        {
            StatusText = $"'{id}' already exists.";
            return;
        }

        string assetsDir = Path.Combine(Session.Folder, "assets");
        Directory.CreateDirectory(assetsDir);
        string fileName = Path.GetFileName(pngPath);
        File.Copy(pngPath, Path.Combine(assetsDir, fileName), overwrite: true);

        var sheet = Assets.SheetImporter.Import(
            id, Path.Combine(assetsDir, fileName), $"assets/{fileName}", Session.Settings.TileSize);
        Session.Add(sheet);
        RebuildNav();
        RefreshValidation();
        StatusText = $"Imported '{id}' ({sheet.Cells.Count} cells).";
    }

    [RelayCommand]
    private void OpenTypeChart()
    {
        if (Session is null) return;
        if (Documents.OfType<TypeChartDocument>().FirstOrDefault() is { } existing)
        {
            ActiveDocument = existing;
            return;
        }
        var doc = new TypeChartDocument(Session);
        doc.Undo.Changed += RefreshValidation;
        Documents.Add(doc);
        ActiveDocument = doc;
    }

    // --- Testable core ---

    public bool OpenProject(string folder)
    {
        ProjectSession? previous = Session;
        try
        {
            Session = ProjectSession.Open(folder);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException
            or InvalidOperationException) // InvalidOperation = locked by another Creator (§10.3)
        {
            StatusText = $"Could not open project: {ex.Message}";
            return false;
        }

        // Release the old project's lock only once the new one opened — but not when reopening the
        // same folder, where "previous" and the new session share one lock file.
        if (previous is not null && !string.Equals(previous.Folder, Session.Folder, StringComparison.OrdinalIgnoreCase))
            previous.Close();

        _recent.Add(folder);
        SyncRecent();

        Documents.Clear();
        ActiveDocument = null;
        ProjectName = Session.Settings.Name;
        RebuildNav();
        RefreshValidation();
        StatusText = Session.RolledBackInterruptedSave
            ? $"Opened {Session.Settings.Name} — an interrupted save from a previous session was rolled back."
            : $"Opened {Session.Settings.Name}.";
        OnPropertyChanged(nameof(HasProject));
        return true;
    }

    public void NewProject(NewProjectRequest request)
    {
        ProjectFile.Save(request.Folder, new ProjectSettings { Name = request.Name, TileSize = request.TileSize });
        OpenProject(request.Folder);
    }

    public void SaveAll()
    {
        if (Session is null) return;
        try
        {
            Session.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The transaction rolled back: disk is untouched, edits are intact, retry is safe.
            StatusText = $"Save failed (project unchanged on disk): {ex.Message}";
            return;
        }
        foreach (EditorDocument doc in Documents) doc.MarkSaved();
        RefreshValidation();
        StatusText = "Saved.";
    }

    private void SyncRecent()
    {
        Recent.Clear();
        foreach (string folder in _recent.Folders)
            Recent.Add(folder);
    }

    public void OpenDocument(EntityId id)
    {
        if (Session is null) return;

        if (Documents.FirstOrDefault(d => d.Id == id) is { } existing)
        {
            ActiveDocument = existing;
            return;
        }

        EditorDocument? doc = CreateDocument(id);
        if (doc is null)
        {
            StatusText = $"No editor for '{id.Category}' yet.";
            return;
        }

        doc.Undo.Changed += RefreshValidation; // live validation strip
        Documents.Add(doc);
        ActiveDocument = doc;
    }

    public void CloseDocument(EditorDocument doc)
    {
        Documents.Remove(doc);
        if (ReferenceEquals(ActiveDocument, doc))
            ActiveDocument = Documents.LastOrDefault();
    }

    public void NavigateToIssue(ValidationIssue issue)
    {
        if (issue.EntityId is { } id)
            OpenDocument(id);
    }

    public void CreateEntity(EntityCategory category, string slug)
    {
        if (Session is null) return;
        if (!EntityId.IsValidSlug(slug))
        {
            StatusText = $"Invalid slug '{slug}' (use a-z, 0-9, _).";
            return;
        }

        var id = new EntityId(category, slug);
        if (Session.Contains(id))
        {
            StatusText = $"'{id}' already exists.";
            return;
        }

        if (NewEntityOf(id) is not { } entity)
        {
            StatusText = $"Can't create a {category} yet — create a type first.";
            return;
        }

        Session.Add(entity);
        RebuildNav();
        OpenDocument(id);
        RefreshValidation();
        StatusText = $"Created '{id}'.";
    }

    public void DuplicateEntity(EntityId source, string newSlug)
    {
        if (Session is null) return;
        if (!EntityId.IsValidSlug(newSlug))
        {
            StatusText = $"Invalid slug '{newSlug}'.";
            return;
        }

        var newId = new EntityId(source.Category, newSlug);
        if (Session.Contains(newId))
        {
            StatusText = $"'{newId}' already exists.";
            return;
        }
        if (Session.Get(source) is not { } original) return;

        // Deep copy via JSON with the id swapped — works for any concrete entity type.
        var node = System.Text.Json.Nodes.JsonNode.Parse(CgmJson.SerializeEntity(original))!.AsObject();
        node["id"] = newId.ToString();
        var copy = (IEntity)CgmJson.Deserialize(node.ToJsonString(), original.GetType());

        Session.Add(copy);
        RebuildNav();
        OpenDocument(newId);
        RefreshValidation();
        StatusText = $"Duplicated to '{newId}'.";
    }

    public void DeleteEntity(EntityId id)
    {
        if (Session is null) return;

        IReadOnlyList<EntityId> referencers = FindReferencers(id);
        if (referencers.Count > 0)
        {
            string list = string.Join(", ", referencers.Take(3));
            if (referencers.Count > 3) list += "…";
            StatusText = $"Can't delete '{id}': referenced by {list}.";
            return;
        }

        if (Documents.FirstOrDefault(d => d.Id == id) is { } doc)
            CloseDocument(doc);
        Session.Remove(id);
        RebuildNav();
        RefreshValidation();
        StatusText = $"Deleted '{id}'.";
    }

    public IReadOnlyList<EntityId> FindReferencers(EntityId target)
    {
        if (Session is null) return [];
        return Session.Snapshot().Entities
            .Where(e => !e.Id.Equals(target) && EntityReferences.Collect(e).Contains(target))
            .Select(e => e.Id)
            .ToList();
    }

    private IEntity? NewEntityOf(EntityId id) => id.Category switch
    {
        EntityCategory.Type => new TypeDef { Id = id, Name = id.Slug },
        EntityCategory.Item => new Item { Id = id, Name = id.Slug, Pocket = Session!.Settings.Pockets.FirstOrDefault() ?? "items" },
        EntityCategory.Ability => new Ability { Id = id, Name = id.Slug },
        EntityCategory.Move when Session!.All<TypeDef>().FirstOrDefault() is { } t =>
            new Move { Id = id, Name = id.Slug, Type = t.Id, DamageClass = DamageClass.Status, Pp = 5 },
        EntityCategory.Species when Session!.All<TypeDef>().FirstOrDefault() is { } t =>
            new Species
            {
                Id = id,
                Name = id.Slug,
                Types = [t.Id],
                BaseStats = new Stats(45, 45, 45, 45, 45, 45),
                GrowthRate = "medium-fast",
            },
        _ => null,
    };

    public void RefreshValidation()
    {
        // Every edit funnels through here (undo-stack Changed + entity ops), so it doubles as the
        // autosave inactivity marker: the 120 s clock restarts and the next tick may snapshot.
        _lastEditUtc = DateTime.UtcNow;
        _snapshotUpToDate = false;

        Issues.Clear();
        if (Session is not null)
            foreach (ValidationIssue issue in Validator.Run(Session.Snapshot()).Issues)
                Issues.Add(issue);
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(ValidationSummary));
    }

    private EditorDocument? CreateDocument(EntityId id) => id.Category switch
    {
        EntityCategory.Move when Session!.Find<Move>(id) is { } m => new MoveDocument(Session, m),
        EntityCategory.Item when Session!.Find<Item>(id) is { } i => new ItemDocument(Session, i),
        EntityCategory.Ability when Session!.Find<Ability>(id) is { } a => new AbilityDocument(Session, a),
        EntityCategory.Species when Session!.Find<Species>(id) is { } s => new SpeciesDocument(Session, s),
        _ => null,
    };

    private void RebuildNav()
    {
        Nav.Clear();
        if (Session is null) return;

        foreach (var group in Session.Snapshot().Entities
                     .GroupBy(e => e.Id.Category)
                     .OrderBy(g => g.Key.ToString(), StringComparer.Ordinal))
        {
            var category = new NavCategory(group.Key.ToString());
            foreach (IEntity e in group.OrderBy(e => e.Id.Slug, StringComparer.Ordinal))
                category.Entities.Add(new NavEntity(e.Id, e.Id.Slug));
            Nav.Add(category);
        }
    }
}
