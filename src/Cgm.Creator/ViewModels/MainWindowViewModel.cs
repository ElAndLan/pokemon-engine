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

    // Keeps HasProject/HasDocuments-driven UI (welcome screen, toolbar enablement, empty hint) in
    // sync whenever the session or the open-document set changes.
    partial void OnSessionChanged(ProjectSession? value) => OnPropertyChanged(nameof(HasProject));
    partial void OnActiveDocumentChanged(EditorDocument? value) => OnPropertyChanged(nameof(HasActiveDocument));

    public ObservableCollection<NavCategory> Nav { get; } = [];
    public ObservableCollection<EditorDocument> Documents { get; } = [];
    public ObservableCollection<ValidationIssue> Issues { get; } = [];
    public ObservableCollection<string> Recent { get; } = [];

    /// <summary>Undo history for session-level operations that span entities (safe delete with
    /// replacement, §10.7/§10.9). Document edits stay on their own per-document stacks; Ctrl+Z
    /// falls back here when the active document has nothing to undo.</summary>
    public Editing.UndoStack SessionUndo { get; } = new();

    public bool HasProject => Session is not null;
    public bool HasActiveDocument => ActiveDocument is not null;
    public int ErrorCount => Issues.Count(i => i.Severity == ValidationSeverity.Error);
    public int WarningCount => Issues.Count(i => i.Severity == ValidationSeverity.Warning);
    public string ValidationSummary => $"{ErrorCount} errors, {WarningCount} warnings";

    /// <summary>Entity categories the Creator can currently create from the UI (Phase 3).</summary>
    public IReadOnlyList<EntityCategory> CreatableCategories { get; } =
        [EntityCategory.Type, EntityCategory.Item, EntityCategory.Move, EntityCategory.Ability,
         EntityCategory.Species, EntityCategory.Tileset, EntityCategory.Map];

    /// <summary>Categories always shown in the nav tree (even when empty), so the tree answers
    /// "where do sprite sheets / maps go?" at a glance. Asset categories are populated by import.</summary>
    private static readonly EntityCategory[] ShownCategories =
    [
        EntityCategory.Map, EntityCategory.Tileset, EntityCategory.Sheet, EntityCategory.Anim,
        EntityCategory.Sound, EntityCategory.Species, EntityCategory.Move, EntityCategory.Item,
        EntityCategory.Ability, EntityCategory.Type, EntityCategory.Encounter, EntityCategory.Trainer,
    ];

    [ObservableProperty] private EntityCategory _newCategory = EntityCategory.Move;

    // --- Nav / document context commands (from the tree and tab UI) ---

    /// <summary>Opens the entity in a document tab (double-click / context Open).</summary>
    [RelayCommand]
    private void OpenEntity(EntityId id) => OpenDocument(id);

    /// <summary>Creates a new entity in a category picked from the tree's context menu, prompting
    /// for a slug. Asset categories (sheets/sounds) route to their importers instead.</summary>
    [RelayCommand]
    private async Task NewInCategoryAsync(NavCategory category)
    {
        if (Session is null || category is null) return;
        switch (category.Category)
        {
            case EntityCategory.Sheet: await ImportSheetAsync(); return;
            case EntityCategory.Sound: await ImportSoundAsync(); return;
        }
        if (await _dialogs.PromptTextAsync($"New {category.Name} — id slug (a-z, 0-9, _):", "") is { } slug)
            CreateEntity(category.Category, slug);
    }

    [RelayCommand]
    private async Task DuplicateEntityCmdAsync(EntityId id)
    {
        if (await _dialogs.PromptTextAsync("Duplicate as — new id slug:", id.Slug + "_copy") is { } slug)
            DuplicateEntity(id, slug);
    }

    [RelayCommand]
    private async Task DeleteEntityCmdAsync(EntityId id) => await DeleteEntityAsync(id);

    /// <summary>Closes one document tab (× button / Ctrl+W / middle-click).</summary>
    [RelayCommand]
    private void CloseDocumentCmd(EditorDocument doc)
    {
        if (doc is not null)
            CloseDocument(doc);
    }

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

    /// <summary>Creates an animation from sheet cells in selection order (17B grouping), prompting
    /// for a slug; frames start at 150 ms and are tuned in the animation editor.</summary>
    public async Task<bool> CreateAnimationAsync(IReadOnlyList<EntityId> sprites)
    {
        if (Session is null || sprites.Count == 0) return false;
        if (await _dialogs.PromptTextAsync("Animation id slug:", "anim") is not { } slug) return false;
        if (!EntityId.IsValidSlug(slug))
        {
            StatusText = $"Invalid slug '{slug}'.";
            return false;
        }
        var id = new EntityId(EntityCategory.Anim, slug);
        if (Session.Contains(id))
        {
            StatusText = $"'{id}' already exists.";
            return false;
        }

        Session.Add(new Animation
        {
            Id = id,
            Name = slug,
            Frames = sprites.Select(s => new AnimFrame(s, 150)).ToList(),
        });
        RebuildNav();
        RefreshValidation();
        OpenDocument(id);
        StatusText = $"Created '{id}' with {sprites.Count} frame(s).";
        return true;
    }

    /// <summary>The 12-cell walk-clip shortcut: a standard 3-frame × 4-direction character sheet
    /// becomes four looping clips via <see cref="Assets.CharacterAnimation"/>.</summary>
    public bool CreateWalkClips(EntityId sheetId)
    {
        if (Session?.Find<SpriteSheet>(sheetId) is not { Cells.Count: 12 } sheet)
        {
            StatusText = "Walk clips need a sheet with exactly 12 cells (3 frames × 4 directions).";
            return false;
        }

        List<Animation> clips;
        try
        {
            clips = Assets.CharacterAnimation.BuildWalkClips(sheet.Id.Slug,
                sheet.Cells.Select(c => c.SpriteId).ToList()).Values.ToList();
        }
        catch (ArgumentException ex)
        {
            StatusText = $"Walk clips failed: {ex.Message}";
            return false;
        }
        if (clips.Any(c => Session.Contains(c.Id)))
        {
            StatusText = "Walk clips already exist for this sheet.";
            return false;
        }

        foreach (Animation clip in clips)
            Session.Add(clip);
        RebuildNav();
        RefreshValidation();
        StatusText = $"Created {clips.Count} walk clips for '{sheet.Id}'.";
        return true;
    }

    [RelayCommand]
    private async Task ImportSoundAsync()
    {
        if (Session is null) return;
        if (await _dialogs.PickWavAsync() is not { } wav) return;
        string suggested = Path.GetFileNameWithoutExtension(wav).ToLowerInvariant();
        if (await _dialogs.PromptTextAsync("Sound id slug:", suggested) is { } slug)
            ImportSound(wav, slug);
    }

    /// <summary>The audio arm of the 17B import transaction: container-validate before any copy,
    /// collision-free file names under assets/audio/, SHA-256 into ContentHash. Kind defaults to
    /// SFX; the sound editor sets music/loop/volume.</summary>
    public bool ImportSound(string wavPath, string slug)
    {
        if (Session is null) return false;
        if (!EntityId.IsValidSlug(slug))
        {
            StatusText = $"Invalid slug '{slug}'.";
            return false;
        }
        var id = new EntityId(EntityCategory.Sound, slug);
        if (Session.Contains(id))
        {
            StatusText = $"'{id}' already exists.";
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(wavPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = $"Import failed: {ex.Message}";
            return false;
        }
        if (!Assets.WavProbe.LooksLikeWave(bytes, out string? error))
        {
            StatusText = $"Import failed — {error}";
            return false;
        }

        string audioDir = Path.Combine(Session.Folder, "assets", "audio");
        Directory.CreateDirectory(audioDir);
        string baseName = Path.GetFileNameWithoutExtension(wavPath);
        string fileName = baseName + ".wav";
        if (File.Exists(Path.Combine(audioDir, fileName)))
            fileName = $"{baseName}_{slug}.wav";
        for (int n = 2; File.Exists(Path.Combine(audioDir, fileName)); n++)
            fileName = $"{baseName}_{slug}_{n}.wav";
        File.WriteAllBytes(Path.Combine(audioDir, fileName), bytes);

        Session.Add(new Sound
        {
            Id = id,
            Name = slug,
            Asset = $"assets/audio/{fileName}",
            ContentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),
        });
        RebuildNav();
        RefreshValidation();
        OpenDocument(id);
        StatusText = $"Imported '{id}'.";
        return true;
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

    /// <summary>Import a PNG straight into a paintable tileset (sheet + tileset in one step).</summary>
    [RelayCommand]
    private async Task ImportTilesetAsync()
    {
        if (Session is null) return;
        if (await _dialogs.PickPngAsync() is not { } png) return;
        string suggested = Path.GetFileNameWithoutExtension(png).ToLowerInvariant();
        if (await _dialogs.PromptTextAsync("Tileset id slug (shared by its sheet):", suggested) is { } slug)
            ImportSheetAsTileset(png, slug);
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

    private static Cgm.Core.Model.Rect? CellRect(SpriteSheet sheet, SheetCell cell) =>
        Assets.SheetBuilder.ResolveRect(sheet, cell);

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

        if (AddImportedSheet(pngPath, id, forceTileGrid: false) is not { } sheet)
            return false;
        RebuildNav();
        RefreshValidation();
        StatusText = $"Imported '{id}' ({sheet.Cells.Count} cells).{GridFitNote(sheet)}";
        return true;
    }

    /// <summary>Imports a PNG and builds a ready-to-paint tileset from it in one step: the sheet is
    /// diced on the exact project tile grid (any sheet size → uniform tile cells), and every
    /// non-blank cell becomes a tile with its sprite assigned. Lands in the tileset editor so only
    /// the gameplay flags (solid/grass/…) remain to set. Sheet and tileset share the slug.</summary>
    public bool ImportSheetAsTileset(string pngPath, string slug)
    {
        if (Session is null) return false;
        if (!EntityId.IsValidSlug(slug))
        {
            StatusText = $"Invalid slug '{slug}'.";
            return false;
        }
        var sheetId = new EntityId(EntityCategory.Sheet, slug);
        var tilesetId = new EntityId(EntityCategory.Tileset, slug);
        if (Session.Contains(sheetId)) { StatusText = $"'{sheetId}' already exists."; return false; }
        if (Session.Contains(tilesetId)) { StatusText = $"'{tilesetId}' already exists."; return false; }

        if (AddImportedSheet(pngPath, sheetId, forceTileGrid: true) is not { } sheet)
            return false;

        // One tile per non-blank sprite cell, in reading order, sprite assigned, flags at defaults.
        Session.Add(new Tileset
        {
            Id = tilesetId,
            Name = slug,
            Tiles = sheet.Cells.Select(c => new Tile { Sprite = c.SpriteId }).ToList(),
        });
        RebuildNav();
        RefreshValidation();
        OpenDocument(tilesetId);
        StatusText = $"Built tileset '{tilesetId}' with {sheet.Cells.Count} tiles — set each tile's "
            + $"flags (solid/grass/…).{GridFitNote(sheet)}";
        return true;
    }

    /// <summary>Shared body of the import transaction: validate-before-copy, collision-free asset
    /// name, SHA-256, slice. Adds the sheet to the session and returns it, or null on failure
    /// (status set). Does not touch nav/validation/status beyond the failure message.</summary>
    private SpriteSheet? AddImportedSheet(string pngPath, EntityId id, bool forceTileGrid)
    {
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
            return null;
        }

        // One asset file per sheet: never silently overwrite another sheet's pixels.
        string assetsDir = Path.Combine(Session!.Folder, "assets");
        Directory.CreateDirectory(assetsDir);
        string baseName = Path.GetFileNameWithoutExtension(pngPath);
        string fileName = baseName + ".png";
        if (File.Exists(Path.Combine(assetsDir, fileName)))
            fileName = $"{baseName}_{id.Slug}.png";
        for (int n = 2; File.Exists(Path.Combine(assetsDir, fileName)); n++)
            fileName = $"{baseName}_{id.Slug}_{n}.png";

        File.WriteAllBytes(Path.Combine(assetsDir, fileName), bytes);

        SpriteSheet sheet = Assets.SheetImporter.Import(
            id, image, $"assets/{fileName}", Session.Settings.TileSize,
            forceCell: forceTileGrid ? Session.Settings.TileSize : null) with
        {
            ContentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),
        };
        Session.Add(sheet);
        return sheet;
    }

    /// <summary>A gentle note when a grid-sliced sheet's pixel size is not a multiple of the tile
    /// size, so the uncovered edge strip is flagged at import instead of silently mis-slicing.</summary>
    private string GridFitNote(SpriteSheet sheet)
    {
        int ts = Session!.Settings.TileSize;
        if (sheet.Mode != SliceMode.Grid || ts <= 0 || (sheet.ImageW % ts == 0 && sheet.ImageH % ts == 0))
            return "";
        return $"  ⚠ {sheet.ImageW}×{sheet.ImageH} isn't a multiple of {ts}px — the right/bottom edge "
            + "isn't fully covered by the grid.";
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

    /// <summary>Picks a sprite via the shared reference picker — used by the tileset and map
    /// editors to assign tile art (MAP_EDITOR_SPEC 17C).</summary>
    public Task<EntityId?> PickSpriteAsync(IReadOnlyList<(EntityId Id, string Name)> candidates) =>
        _dialogs.PickEntityAsync(EntityCategory.Sprite, candidates, "Pick a sprite:");

    /// <summary>A single-line text prompt, exposed for editors that configure a free-text field
    /// (a sign's text, a slug) from the canvas.</summary>
    public Task<string?> PromptTextAsync(string prompt, string initial) =>
        _dialogs.PromptTextAsync(prompt, initial);

    /// <summary>Picks any entity of a category via the shared picker (map picker for warp targets,
    /// encounter tables, objects, etc.).</summary>
    public Task<EntityId?> PickEntityAsync(EntityCategory category, string prompt)
    {
        var candidates = Session is null ? [] : Session.Snapshot().Entities
            .Where(e => e.Id.Category == category)
            .Select(e => (e.Id, DisplayName(e)))
            .ToList();
        return _dialogs.PickEntityAsync(category, candidates, prompt);
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
        EntityCategory.Tileset => new Tileset { Id = id, Name = id.Slug },
        // A new map adopts the first tileset in the project so its palette isn't empty on open;
        // more can be added from the map editor. No tileset yet → empty, and the editor prompts.
        EntityCategory.Map => new Map
        {
            Id = id, Name = id.Slug, Width = 16, Height = 16, Layers = BlankLayers(16, 16),
            Tilesets = Session!.All<Tileset>().Take(1).Select(t => t.Id).ToList(),
        },
        _ => null,
    };

    private static MapLayers BlankLayers(int w, int h) => new()
    {
        Ground = Enumerable.Repeat(-1, w * h).ToList(),
        DecoBelow = Enumerable.Repeat(-1, w * h).ToList(),
        DecoAbove = Enumerable.Repeat(-1, w * h).ToList(),
    };

    public void RefreshValidation()
    {
        // Every edit funnels through here (undo-stack Changed + entity ops), so it doubles as the
        // autosave inactivity marker: the 120 s clock restarts and the next tick may snapshot.
        _lastEditUtc = DateTime.UtcNow;
        _snapshotUpToDate = false;

        Issues.Clear();
        if (Session is not null)
        {
            foreach (ValidationIssue issue in Validator.Run(Session.Snapshot()).Issues)
                Issues.Add(issue);
            foreach (ValidationIssue issue in AssetDiagnostics())
                Issues.Add(issue);
        }
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(ValidationSummary));
    }

    /// <summary>Asset-file diagnostics (ASSET_PIPELINE_SPEC 17B) — Creator-side because Core
    /// validation never reads the machine's filesystem: a missing asset file, a file whose bytes no
    /// longer match the recorded hash (edited outside the Creator — reimport records the change),
    /// and orphaned files in assets/ nothing references (warning; deleting is the user's call).</summary>
    private readonly Dictionary<string, (DateTime Stamp, long Length, string Hash)> _hashCache = [];

    public IEnumerable<ValidationIssue> AssetDiagnostics()
    {
        if (Session is null)
            yield break;

        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var owners = Session.All<SpriteSheet>().Select(s => (s.Id, s.Asset, s.ContentHash))
            .Concat(Session.All<Sound>().Select(s => (s.Id, s.Asset, s.ContentHash)));

        foreach ((EntityId id, string asset, string? hash) in owners)
        {
            if (string.IsNullOrWhiteSpace(asset))
                continue; // the Core path rule owns empty/unsafe paths
            referenced.Add(asset.Replace('\\', '/'));

            string full = Path.Combine(Session.Folder, asset);
            if (!File.Exists(full))
            {
                yield return new ValidationIssue("asset-file", ValidationSeverity.Error, id,
                    $"Asset file '{asset}' is missing.", "Reimport the asset or restore the file.",
                    Field: "asset");
            }
            else if (hash is not null && Hash(full) != hash)
            {
                yield return new ValidationIssue("asset-file", ValidationSeverity.Error, id,
                    $"Asset file '{asset}' has changed outside the Creator (content hash mismatch).",
                    "Reimport to accept the new pixels, or restore the original file.",
                    Field: "contentHash");
            }
        }

        // Grid-sliced sheets whose pixel size isn't a multiple of the tile size have an uncovered
        // edge strip — flagged so off-grid sheets are visible, not silently mis-sliced.
        int ts = Session.Settings.TileSize;
        if (ts > 0)
            foreach (SpriteSheet sheet in Session.All<SpriteSheet>())
                if (sheet.Mode == SliceMode.Grid && sheet.ImageW > 0 && sheet.ImageH > 0
                    && (sheet.ImageW % ts != 0 || sheet.ImageH % ts != 0))
                    yield return new ValidationIssue("sheet-grid-fit", ValidationSeverity.Warning, sheet.Id,
                        $"Sheet is {sheet.ImageW}×{sheet.ImageH}, not a multiple of the {ts}px tile size; "
                        + "the right/bottom edge isn't fully covered by the grid.",
                        "Crop the sheet to a multiple of the tile size, or slice it with rects instead.");

        string assetsDir = Path.Combine(Session.Folder, "assets");
        if (!Directory.Exists(assetsDir))
            yield break;
        foreach (string file in Directory.EnumerateFiles(assetsDir, "*.*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(Session.Folder, file).Replace('\\', '/');
            if (!referenced.Contains(rel))
                yield return new ValidationIssue("asset-orphan", ValidationSeverity.Warning, null,
                    $"'{rel}' is not referenced by any sheet or sound.",
                    "Import it as an asset or delete the file.");
        }
    }

    /// <summary>SHA-256 with a (stamp, length) cache — validation runs on every debounced edit and
    /// must not re-hash unchanged art each time.</summary>
    private string Hash(string path)
    {
        var info = new FileInfo(path);
        if (_hashCache.TryGetValue(path, out var cached)
            && cached.Stamp == info.LastWriteTimeUtc && cached.Length == info.Length)
            return cached.Hash;

        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
        _hashCache[path] = (info.LastWriteTimeUtc, info.Length, hash);
        return hash;
    }

    private EditorDocument? CreateDocument(EntityId id) => id.Category switch
    {
        EntityCategory.Move when Session!.Find<Move>(id) is { } m => new MoveDocument(Session, m),
        EntityCategory.Item when Session!.Find<Item>(id) is { } i => new ItemDocument(Session, i),
        EntityCategory.Ability when Session!.Find<Ability>(id) is { } a => new AbilityDocument(Session, a),
        EntityCategory.Species when Session!.Find<Species>(id) is { } s => new SpeciesDocument(Session, s),
        EntityCategory.Sheet when Session!.Find<SpriteSheet>(id) is { } sheet => new SheetDocument(Session, sheet),
        EntityCategory.Sound when Session!.Find<Sound>(id) is { } sound => new SoundDocument(Session, sound),
        EntityCategory.Anim when Session!.Find<Animation>(id) is { } anim => new AnimDocument(Session, anim),
        EntityCategory.Tileset when Session!.Find<Tileset>(id) is { } ts => new TilesetDocument(Session, ts),
        EntityCategory.Map when Session!.Find<Map>(id) is { } map => new MapDocument(Session, map),
        _ => null,
    };

    private void RebuildNav()
    {
        Nav.Clear();
        if (Session is null) return;

        // Every creatable category shows as a node even when empty, so "where do new maps go?" has
        // a visible answer and the tree doubles as a checklist of what a project can hold.
        var byCategory = Session.Snapshot().Entities
            .GroupBy(e => e.Id.Category)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => DisplayName(e), StringComparer.OrdinalIgnoreCase).ToList());

        IEnumerable<EntityCategory> categories = ShownCategories
            .Concat(byCategory.Keys)
            .Distinct();

        foreach (EntityCategory cat in categories.OrderBy(c => c.ToString(), StringComparer.Ordinal))
        {
            var node = new NavCategory(cat);
            if (byCategory.TryGetValue(cat, out var entities))
                foreach (IEntity e in entities)
                    node.Entities.Add(new NavEntity(e.Id, DisplayName(e)));
            Nav.Add(node);
        }
    }
}
