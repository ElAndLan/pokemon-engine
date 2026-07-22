using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation;
using Cgm.Creator.Editing;
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

        // Session-level undo (safe delete + rewrites) changes entities without an open document,
        // so the nav tree and validation strip refresh on every stack movement.
        SessionUndo.Changed += () =>
        {
            RebuildNav();
            RefreshValidation();
        };
    }

    [ObservableProperty] private ProjectSession? _session;
    [ObservableProperty] private EditorDocument? _activeDocument;
    [ObservableProperty] private string _statusText = "No project open.";
    [ObservableProperty] private string _projectName = "";

    public ObservableCollection<NavCategory> Nav { get; } = [];
    public ObservableCollection<EditorDocument> Documents { get; } = [];
    public ObservableCollection<ValidationIssue> Issues { get; } = [];
    public ObservableCollection<string> Recent { get; } = [];

    /// <summary>Undo history for session-level operations that span entities (safe delete with
    /// replacement, §10.7/§10.9). Document edits stay on their own per-document stacks; Ctrl+Z
    /// falls back here when the active document has nothing to undo.</summary>
    public Editing.UndoStack SessionUndo { get; } = new();

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
    private void Undo()
    {
        if (ActiveDocument?.Undo.CanUndo == true)
            ActiveDocument.Undo.Undo();
        else
            SessionUndo.Undo();
    }

    [RelayCommand]
    private void Redo()
    {
        if (ActiveDocument?.Undo.CanRedo == true)
            ActiveDocument.Undo.Redo();
        else
            SessionUndo.Redo();
    }

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
    private async Task DeleteActiveAsync()
    {
        if (ActiveDocument?.Id is { } id)
            await DeleteEntityAsync(id);
    }

    [RelayCommand]
    private async Task ImportSheetAsync()
    {
        if (Session is null) return;
        if (await _dialogs.PickPngAsync() is not { } png) return;
        string suggested = Path.GetFileNameWithoutExtension(png).ToLowerInvariant();
        if (await _dialogs.PromptTextAsync("Sheet id slug:", suggested) is not { } slug) return;

        // An existing sheet id offers the reimport path (§17B collision: replace or new slug).
        var id = new EntityId(EntityCategory.Sheet, EntityId.IsValidSlug(slug) ? slug : "invalid");
        if (EntityId.IsValidSlug(slug) && Session.Contains(id))
        {
            if (await _dialogs.ConfirmAsync($"'{id}' already exists. Replace its pixels with this file (reimport)?"))
                await ReimportSheetAsync(id, png);
            else
                StatusText = $"'{id}' already exists — import again with a different slug.";
            return;
        }

        ImportSheet(png, slug);
    }

    /// <summary>The 17B reimport: keeps the sheet's id and authored cells, updates pixels/hash/
    /// dimensions, and removes cells that no longer fit — reported and confirmation-gated before
    /// anything is written. Declining leaves project and file untouched.</summary>
    public async Task<bool> ReimportSheetAsync(EntityId id, string pngPath)
    {
        if (Session?.Find<SpriteSheet>(id) is not { } sheet) return false;

        byte[] bytes;
        Assets.ImageData image;
        try
        {
            bytes = File.ReadAllBytes(pngPath);
            image = Assets.PngDecoder.Decode(bytes); // same trust boundary as import
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            StatusText = $"Reimport failed — not a readable PNG: {ex.Message}";
            return false;
        }

        var invalidated = sheet.Cells
            .Where(c => CellRect(sheet, c) is not { } r
                || r.X + r.W > image.Width || r.Y + r.H > image.Height)
            .ToList();

        string report = invalidated.Count == 0
            ? $"All {sheet.Cells.Count} cells still fit."
            : $"{invalidated.Count} of {sheet.Cells.Count} cells fall outside the new bounds and will be removed: "
              + string.Join(", ", invalidated.Take(5).Select(c => c.SpriteId.Slug))
              + (invalidated.Count > 5 ? ", …" : "");
        if (!await _dialogs.ConfirmAsync(
            $"Reimport '{id}' from {Path.GetFileName(pngPath)} ({image.Width}×{image.Height})? {report}"))
            return false;

        File.WriteAllBytes(Path.Combine(Session.Folder, sheet.Asset), bytes);
        Session.Put(sheet with
        {
            ImageW = image.Width,
            ImageH = image.Height,
            ContentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),
            Cells = sheet.Cells.Except(invalidated).ToList(),
        });
        RefreshValidation();
        StatusText = $"Reimported '{id}'; {invalidated.Count} cell(s) removed.";
        return true;
    }

    /// <summary>A cell's pixel rect: authored rects directly; grid cells from the sheet's original
    /// grid parameters (row-major over the original column count).</summary>
    private static Cgm.Core.Model.Rect? CellRect(SpriteSheet sheet, SheetCell cell)
    {
        if (cell.Rect is { } rect)
            return rect;
        if (cell.Index is not { } index || sheet.CellW <= 0 || sheet.CellH <= 0)
            return null;
        int strideX = sheet.CellW + sheet.SpacingX;
        int columns = Math.Max(1, (sheet.ImageW - sheet.OffsetX + sheet.SpacingX) / strideX);
        return new Cgm.Core.Model.Rect(
            sheet.OffsetX + index % columns * strideX,
            sheet.OffsetY + index / columns * (sheet.CellH + sheet.SpacingY),
            sheet.CellW, sheet.CellH);
    }

    /// <summary>The 17B import transaction (ASSET_PIPELINE_SPEC): decode/validate the source in
    /// place first — a malformed file rejects before anything is copied — then copy the validated
    /// bytes under a collision-free name, hash them, and slice via the suggestion ladder.</summary>
    public bool ImportSheet(string pngPath, string slug)
    {
        if (Session is null) return false;
        if (!EntityId.IsValidSlug(slug))
        {
            StatusText = $"Invalid slug '{slug}'.";
            return false;
        }
        var id = new EntityId(EntityCategory.Sheet, slug);
        if (Session.Contains(id))
        {
            StatusText = $"'{id}' already exists.";
            return false;
        }

        byte[] bytes;
        Assets.ImageData image;
        try
        {
            bytes = File.ReadAllBytes(pngPath);
            // StbImageSharp reports malformed input as a bare Exception; this is the trust
            // boundary where arbitrary user files are rejected, so the catch is deliberately wide.
            image = Assets.PngDecoder.Decode(bytes);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            StatusText = $"Import failed — not a readable PNG: {ex.Message}";
            return false;
        }

        // One asset file per sheet: never silently overwrite another sheet's pixels.
        string assetsDir = Path.Combine(Session.Folder, "assets");
        Directory.CreateDirectory(assetsDir);
        string baseName = Path.GetFileNameWithoutExtension(pngPath);
        string fileName = baseName + ".png";
        if (File.Exists(Path.Combine(assetsDir, fileName)))
            fileName = $"{baseName}_{slug}.png";
        for (int n = 2; File.Exists(Path.Combine(assetsDir, fileName)); n++)
            fileName = $"{baseName}_{slug}_{n}.png";

        File.WriteAllBytes(Path.Combine(assetsDir, fileName), bytes);

        SpriteSheet sheet = Assets.SheetImporter.Import(
            id, image, $"assets/{fileName}", Session.Settings.TileSize) with
        {
            ContentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),
        };
        Session.Add(sheet);
        RebuildNav();
        RefreshValidation();
        StatusText = $"Imported '{id}' ({sheet.Cells.Count} cells).";
        return true;
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

    /// <summary>Opens the issue's document and focuses the named field when the issue carries one
    /// (§10.8); issues without a field fall back to the document.</summary>
    public void NavigateToIssue(ValidationIssue issue)
    {
        if (issue.EntityId is not { } id)
            return;
        OpenDocument(id);
        if (ActiveDocument?.Id == id)
            ActiveDocument.FocusedField = issue.Field;
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

    /// <summary>Deletes an entity per §10.7: unreferenced deletes directly; a referenced entity
    /// shows its usages and offers an explicit same-category replacement, applied as one grouped
    /// undo step — rewrite every reference, then delete. Cancelling the pick deletes nothing.</summary>
    public async Task<bool> DeleteEntityAsync(EntityId id)
    {
        if (Session is null) return false;

        IReadOnlyList<(EntityId Entity, string Field)> usages = FindUsages(id);
        if (usages.Count == 0)
        {
            if (Documents.FirstOrDefault(d => d.Id == id) is { } doc)
                CloseDocument(doc);
            Session.Remove(id);
            RebuildNav();
            RefreshValidation();
            StatusText = $"Deleted '{id}'.";
            return true;
        }

        var candidates = Session.Snapshot().Entities
            .Where(e => e.Id.Category == id.Category && !e.Id.Equals(id))
            .Select(e => (e.Id, DisplayName(e)))
            .ToList();

        EntityId? replacement = await _dialogs.PickEntityAsync(id.Category, candidates,
            $"'{id}' is referenced by {UsageSummary(usages)}. Pick the replacement every reference " +
            "will be rewritten to; the delete and the rewrites are one undo step.");

        if (replacement is not { } target || target.Equals(id) || Session.Get(target) is null)
        {
            StatusText = $"Can't delete '{id}': referenced by {UsageSummary(usages)}.";
            return false;
        }

        SessionUndo.BeginGroup();
        foreach (EntityId referencer in usages.Select(u => u.Entity).Distinct())
        {
            IEntity before = Session.Get(referencer)!;
            IEntity after = RewriteReferences(before, id, target);
            SessionUndo.Push(new SnapshotCommand<IEntity>(before, after, e => Session.Put(e)));
        }
        IEntity deleted = Session.Get(id)!;
        SessionUndo.Push(new SnapshotCommand<IEntity?>(deleted, null,
            e => { if (e is null) Session!.Remove(id); else Session!.Put(e); }));
        SessionUndo.EndGroup();

        if (Documents.FirstOrDefault(d => d.Id == id) is { } open)
            CloseDocument(open);
        RebuildNav();
        RefreshValidation();
        StatusText = $"Deleted '{id}'; {usages.Count} reference(s) now point to '{target}'.";
        return true;
    }

    /// <summary>Every (entity, field-path) holding a reference to the target (§10.7), grouped by
    /// entity in stable order — e.g. <c>species:ember_fox → learnset[3].move</c>.</summary>
    public IReadOnlyList<(EntityId Entity, string Field)> FindUsages(EntityId target)
    {
        if (Session is null) return [];
        return Session.Snapshot().Entities
            .Where(e => !e.Id.Equals(target))
            .SelectMany(e => EntityReferences.CollectWithPaths(e)
                .Where(r => r.Id.Equals(target))
                .Select(r => (e.Id, r.Path)))
            .OrderBy(u => u.Item1.ToString(), StringComparer.Ordinal)
            .ThenBy(u => u.Path, StringComparer.Ordinal)
            .ToList();
    }

    private static string UsageSummary(IReadOnlyList<(EntityId Entity, string Field)> usages)
    {
        string list = string.Join(", ", usages.Take(3).Select(u => $"{u.Entity}.{u.Field}"));
        return usages.Count > 3 ? $"{list}, … ({usages.Count} total)" : list;
    }

    private string DisplayName(IEntity entity) =>
        entity.GetType().GetProperty("Name")?.GetValue(entity) as string ?? entity.Id.Slug;

    /// <summary>Rewrites every reference to <paramref name="from"/> as <paramref name="to"/> via
    /// the same JSON round-trip Duplicate uses — generic over any entity shape. Only whole string
    /// values equal to the id are touched; the root "id" declaration is preserved.</summary>
    private static IEntity RewriteReferences(IEntity entity, EntityId from, EntityId to)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(CgmJson.SerializeEntity(entity))!.AsObject();
        var own = node["id"]!.GetValue<string>();
        Rewrite(node, from.ToString(), to.ToString());
        node["id"] = own;
        return (IEntity)CgmJson.Deserialize(node.ToJsonString(), entity.GetType());
    }

    private static void Rewrite(System.Text.Json.Nodes.JsonNode? node, string from, string to)
    {
        switch (node)
        {
            case System.Text.Json.Nodes.JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (obj[key] is System.Text.Json.Nodes.JsonValue v
                        && v.TryGetValue(out string? s) && s == from)
                        obj[key] = to;
                    else
                        Rewrite(obj[key], from, to);

                    if (key == from) // dictionaries keyed by EntityId serialize ids as property names
                    {
                        System.Text.Json.Nodes.JsonNode? moved = obj[key];
                        obj.Remove(key);
                        obj[to] = moved;
                    }
                }
                break;
            case System.Text.Json.Nodes.JsonArray arr:
                for (int i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is System.Text.Json.Nodes.JsonValue v
                        && v.TryGetValue(out string? s) && s == from)
                        arr[i] = to;
                    else
                        Rewrite(arr[i], from, to);
                }
                break;
        }
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
