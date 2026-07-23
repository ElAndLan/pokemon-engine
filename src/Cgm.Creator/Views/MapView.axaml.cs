using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Cgm.Core.Model;
using Cgm.Creator.ViewModels;
using Rect = Avalonia.Rect;

namespace Cgm.Creator.Views;

/// <summary>Map canvas glue: composites the visible tile layers into one bitmap (redrawn per edit),
/// applies integer zoom, hosts the tool palette, and translates pointer strokes into the document's
/// stroke ops. All authoring state lives in <see cref="MapDocument"/>.</summary>
public partial class MapView : UserControl
{
    private SpriteBitmaps? _bitmaps;
    private RenderTargetBitmap? _renderTarget;
    private MapDocument? _subscribed;
    private bool _stroking;
    private (int X, int Y) _anchor;

    public MapView()
    {
        InitializeComponent();
        RenderOptions.SetBitmapInterpolationMode(MapImage, BitmapInterpolationMode.None);
        ToolBox.ItemsSource = Enum.GetValues<MapTool>();
        ToolBox.SelectedIndex = 0;
        LayerBox.ItemsSource = Enum.GetValues<MapLayerId>();
        LayerBox.SelectedIndex = 0;
        ModeBox.ItemsSource = Enum.GetValues<MapEditMode>();
        ModeBox.SelectedIndex = 0;
        CollisionValueBox.ItemsSource = Enum.GetValues<CollisionValue>();
        CollisionValueBox.SelectedItem = CollisionValue.Solid;
        EntityKindBox.ItemsSource = Enum.GetValues<EntityKind>();
        EntityKindBox.SelectedIndex = 0;
        DataContextChanged += (_, _) => SubscribeAndRebuild();
        DetachedFromVisualTree += (_, _) => UnsubscribeAndDispose();
        ApplyZoom();
    }

    private MapDocument? Doc => DataContext as MapDocument;

    private MainWindowViewModel? Shell => this.FindAncestorOfType<Window>()?.DataContext as MainWindowViewModel;

    private void SubscribeAndRebuild()
    {
        if (_subscribed is not null)
        {
            _subscribed.Undo.Changed -= OnDocumentChanged;
            _subscribed.Session.Changed -= OnSessionChanged;
        }
        _subscribed = Doc;
        if (_subscribed is not null)
        {
            _subscribed.Undo.Changed += OnDocumentChanged;
            _subscribed.Session.Changed += OnSessionChanged;
        }
        Rebuild();
    }

    private void OnDocumentChanged()
    {
        BuildPalette();
        Redraw();
    }

    private void OnSessionChanged(EntityId? id)
    {
        if (id is null || id.Value.Category is EntityCategory.Tileset or EntityCategory.Sheet)
            Rebuild();
    }

    private void UnsubscribeAndDispose()
    {
        if (_subscribed is not null)
        {
            _subscribed.Undo.Changed -= OnDocumentChanged;
            _subscribed.Session.Changed -= OnSessionChanged;
            _subscribed = null;
        }
        _bitmaps?.Dispose();
        _renderTarget?.Dispose();
    }

    private void Rebuild()
    {
        _bitmaps?.Dispose();
        _bitmaps = Doc is { } doc ? new SpriteBitmaps(doc.Session) : null;
        BuildPalette();
        Redraw();
    }

    private void BuildPalette()
    {
        Palette.Children.Clear();
        if (Doc is not { } doc)
            return;

        EmptyPaletteHint.IsVisible = doc.PaletteTiles.Count == 0;
        TilesetLabel.Text = doc.MapTilesets.Count == 0
            ? "no tileset"
            : string.Join(", ", doc.MapTilesets.Select(t => t.Slug));

        foreach (PaletteTile tile in doc.PaletteTiles)
        {
            var image = new Image { Width = 32, Height = 32, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
            if (tile.Sprite is { } sprite)
                image.Source = _bitmaps?.Crop(sprite);

            var border = new Border
            {
                Width = 46, Height = 58, Margin = new(2),
                BorderBrush = tile.Index == doc.SelectedTile ? Brushes.DodgerBlue : Brushes.Transparent,
                BorderThickness = new(2),
                Child = new StackPanel
                {
                    Children =
                    {
                        image,
                        new TextBlock
                        {
                            Text = tile.Index.ToString(),
                            FontSize = 10,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Opacity = 0.7,
                        },
                    },
                },
            };
            ToolTip.SetTip(border, $"{tile.Tileset.Slug} tile {tile.LocalIndex} · global {tile.Index}");
            int index = tile.Index;
            border.PointerPressed += (_, _) => { doc.SelectedTile = index; BuildPalette(); };
            Palette.Children.Add(border);
        }
    }

    /// <summary>Composites the visible layers to one bitmap at native pixel size. Cheap enough to
    /// redraw per edit at demo map sizes; a dirty-chunk cache is the upgrade path if it ever drags.</summary>
    // ponytail: full redraw per edit; cache per 32x32 chunk when 256^2 maps feel heavy.
    private void Redraw()
    {
        if (Doc is not { } doc || doc.Width <= 0 || doc.Height <= 0)
        {
            MapImage.Source = null;
            return;
        }

        int ts = doc.TileSize;
        var target = new RenderTargetBitmap(new PixelSize(doc.Width * ts, doc.Height * ts));
        using (DrawingContext ctx = target.CreateDrawingContext())
        {
            DrawLayer(ctx, doc, MapLayerId.Ground, ShowGround.IsChecked == true, ts);
            DrawLayer(ctx, doc, MapLayerId.DecoBelow, ShowBelow.IsChecked == true, ts);
            DrawLayer(ctx, doc, MapLayerId.DecoAbove, ShowAbove.IsChecked == true, ts);
            DrawOverlays(ctx, doc, ts);
            DrawEntities(ctx, doc, ts);
            DrawGrid(ctx, doc, ts);
        }
        MapImage.Source = target;
        _renderTarget?.Dispose();
        _renderTarget = target;
    }

    private void DrawGrid(DrawingContext ctx, MapDocument doc, int ts)
    {
        if (GridToggle.IsChecked != true)
            return;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF)));
        for (int x = 0; x <= doc.Width; x++)
            ctx.DrawLine(pen, new Point(x * ts, 0), new Point(x * ts, doc.Height * ts));
        for (int y = 0; y <= doc.Height; y++)
            ctx.DrawLine(pen, new Point(0, y * ts), new Point(doc.Width * ts, y * ts));
    }

    private void DrawLayer(DrawingContext ctx, MapDocument doc, MapLayerId layer, bool visible, int ts)
    {
        if (!visible)
            return;
        for (int y = 0; y < doc.Height; y++)
            for (int x = 0; x < doc.Width; x++)
            {
                int index = doc.LayerForRender(layer)[y * doc.Width + x];
                if (index < 0 || doc.SpriteFor(index) is not { } sprite || _bitmaps?.Crop(sprite) is not { } bmp)
                    continue;
                ctx.DrawImage(bmp, new Rect(x * ts, y * ts, ts, ts));
            }
    }

    private void DrawOverlays(DrawingContext ctx, MapDocument doc, int ts)
    {
        bool collision = CollisionToggle.IsChecked == true;
        bool encounter = EncounterToggle.IsChecked == true;
        if (!collision && !encounter)
            return;

        var solid = new SolidColorBrush(Color.FromArgb(0x60, 0xE0, 0x40, 0x40));
        var grass = new SolidColorBrush(Color.FromArgb(0x50, 0x40, 0xE0, 0x60));
        for (int y = 0; y < doc.Height; y++)
            for (int x = 0; x < doc.Width; x++)
            {
                var cell = new Rect(x * ts, y * ts, ts, ts);
                if (collision && doc.CollisionAt(x, y) is not null)
                    ctx.FillRectangle(solid, cell);
                if (encounter && doc.EncounterAt(x, y) is not null)
                    ctx.FillRectangle(grass, cell);
            }
    }

    /// <summary>Entity markers: a labelled dot per placed entity, highlighted when selected. Full
    /// sprite rendering of NPCs/objects is deferred; the marker is enough to place and address them.</summary>
    private void DrawEntities(DrawingContext ctx, MapDocument doc, int ts)
    {
        if (doc.EditMode != MapEditMode.Entities)
            return;
        var pen = new Pen(Brushes.White);
        foreach (MapEntity entity in doc.Entities)
        {
            var cell = new Rect(entity.Pos.X * ts, entity.Pos.Y * ts, ts, ts);
            bool selected = entity.Key == _selectedEntity;
            ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(selected ? (byte)0xC0 : (byte)0x80, 0x30, 0x60, 0xE0)), cell);
            if (selected)
                ctx.DrawRectangle(null, pen, cell);
        }
    }

    // --- Pointer strokes ---

    private (int X, int Y)? CellAt(PointerEventArgs e)
    {
        if (Doc is not { } doc)
            return null;
        Point p = e.GetPosition(MapImage);
        int x = (int)(p.X / doc.TileSize), y = (int)(p.Y / doc.TileSize);
        return x >= 0 && y >= 0 && x < doc.Width && y < doc.Height ? (x, y) : null;
    }

    private string? _selectedEntity;

    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(MapImage).Properties.IsLeftButtonPressed)
            return;
        if (Doc is not { } doc || CellAt(e) is not { } cell)
            return;
        if (doc.EditMode == MapEditMode.Tiles && ActiveLayerUnavailable(doc.ActiveLayer))
        {
            if (Shell is { } shell)
                shell.StatusText = "The active layer is hidden or locked; show/unlock it before painting.";
            return;
        }

        // Entity mode: click an existing entity to select it, else place the chosen kind.
        if (doc.EditMode == MapEditMode.Entities)
        {
            if (doc.EntityAt(cell.X, cell.Y) is { } hit)
                _selectedEntity = hit.Key;
            else if (EntityKindBox.SelectedItem is EntityKind kind)
                _selectedEntity = doc.Place(NewEntity(kind, cell.X, cell.Y));
            UpdateEntityLabel();
            Redraw();
            return;
        }

        if (doc.EditMode == MapEditMode.Collision)
        {
            doc.SetCollision(cell.X, cell.Y, doc.CollisionAt(cell.X, cell.Y) is null ? doc.SelectedCollision : null);
            Redraw();
            return;
        }
        if (doc.EditMode == MapEditMode.Encounters)
        {
            doc.SetEncounter(cell.X, cell.Y, doc.EncounterAt(cell.X, cell.Y) is null ? doc.SelectedEncounterTable : null);
            Redraw();
            return;
        }

        _stroking = true;
        _anchor = cell;
        doc.BeginStroke();
        doc.StrokePaint(cell.X, cell.Y, _anchor.X, _anchor.Y);
        Redraw();
        e.Pointer.Capture(MapImage);
    }

    private void OnCanvasMoved(object? sender, PointerEventArgs e)
    {
        if (CellAt(e) is { } hover)
            CursorLabel.Text = $"{hover.X},{hover.Y}";
        if (!_stroking || Doc is not { } doc || CellAt(e) is not { } cell)
            return;
        doc.StrokePaint(cell.X, cell.Y, _anchor.X, _anchor.Y);
        Redraw();
    }

    private void OnCanvasReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_stroking || Doc is not { } doc)
            return;
        _stroking = false;
        doc.EndStroke();
        BuildPalette(); // eyedropper may have changed the selection
        Redraw();
        e.Pointer.Capture(null);
    }

    private void OnCanvasCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_stroking || Doc is not { } doc)
            return;
        _stroking = false;
        doc.CancelStroke();
        Redraw();
    }

    // --- Toolbar ---

    private void OnToolChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Doc is { } doc && ToolBox.SelectedItem is MapTool tool)
            doc.Tool = tool;
    }

    private void OnLayerChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Doc is { } doc && LayerBox.SelectedItem is MapLayerId layer)
            doc.ActiveLayer = layer;
    }

    private void OnModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Doc is { } doc && ModeBox.SelectedItem is MapEditMode mode)
        {
            doc.EditMode = mode;
            Redraw();
        }
    }

    private void OnCollisionValueChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Doc is { } doc && CollisionValueBox.SelectedItem is CollisionValue value)
            doc.SelectedCollision = value;
    }

    private void OnZoomChanged(object? sender, RoutedEventArgs e) => ApplyZoom();

    private void ApplyZoom()
    {
        double zoom = ZoomSlider?.Value ?? 1;
        ZoomHost.LayoutTransform = new ScaleTransform(zoom, zoom);
        if (ZoomLabel is not null)
            ZoomLabel.Text = $"{zoom * 100:0}%";
    }

    private void OnOverlayToggled(object? sender, RoutedEventArgs e) => Redraw();

    private void OnRedraw(object? sender, RoutedEventArgs e) => Redraw();

    private bool ActiveLayerUnavailable(MapLayerId layer) => layer switch
    {
        MapLayerId.Ground => ShowGround.IsChecked != true || LockGround.IsChecked == true,
        MapLayerId.DecoBelow => ShowBelow.IsChecked != true || LockBelow.IsChecked == true,
        _ => ShowAbove.IsChecked != true || LockAbove.IsChecked == true,
    };

    private async void OnPickEncounter(object? sender, RoutedEventArgs e)
    {
        if (Doc is not { } doc || Shell is not { } shell)
            return;
        if (await shell.PickEntityAsync(EntityCategory.Encounter, "Paint with encounter table:") is { } table)
        {
            doc.SelectedEncounterTable = table;
            EncounterLabel.Text = table.Slug;
        }
    }

    /// <summary>Assigns a tileset to the map so its tiles fill the palette. The picked tileset's
    /// tiles append to the global index space, so existing painted tiles keep their meaning.</summary>
    private async void OnAddTileset(object? sender, RoutedEventArgs e)
    {
        if (Doc is not { } doc || Shell is not { } shell)
            return;
        if (!doc.AvailableTilesets.Any())
        {
            shell.StatusText = "No tilesets in the project yet — create one first (File ▸ Import as Tileset).";
            return;
        }
        if (await shell.PickEntityAsync(Cgm.Core.Model.EntityCategory.Tileset, "Add a tileset to this map:") is { } id)
        {
            doc.AddTileset(id); // no-op if already on the map
            BuildPalette();
            Redraw();
        }
    }

    // --- Entity actions ---

    private static MapEntity NewEntity(EntityKind kind, int x, int y)
    {
        var pos = new GridPos(x, y);
        return kind switch
        {
            EntityKind.PlayerStart => new PlayerStartEntity { Pos = pos },
            EntityKind.Npc => new NpcEntity { Pos = pos },
            EntityKind.Warp => new WarpEntity { Pos = pos },
            EntityKind.Pickup => new PickupEntity { Pos = pos },
            EntityKind.Sign => new SignEntity { Pos = pos },
            EntityKind.Trigger => new TriggerEntity { Pos = pos },
            _ => new SignEntity { Pos = pos },
        };
    }

    private void UpdateEntityLabel() =>
        EntityLabel.Text = _selectedEntity is { } key ? $"selected: {key}" : "";

    private void OnDeleteEntity(object? sender, RoutedEventArgs e)
    {
        if (Doc is { } doc && _selectedEntity is { } key)
        {
            doc.DeleteEntity(key);
            _selectedEntity = null;
            UpdateEntityLabel();
            Redraw();
        }
    }

    /// <summary>The essential per-instance config reachable from the canvas: a sign's text, a
    /// warp's target map + tile. Full structured forms for every field are 17D.</summary>
    private async void OnConfigureEntity(object? sender, RoutedEventArgs e)
    {
        if (Doc is not { } doc || _selectedEntity is not { } key
            || doc.Entities.FirstOrDefault(en => en.Key == key) is not { } entity
            || Shell is not { } shell)
            return;

        switch (entity)
        {
            case SignEntity sign when await shell.PromptTextAsync("Sign text:", sign.Text) is { } text:
                doc.ConfigureEntity(sign with { Text = text });
                break;
            case WarpEntity warp when await shell.PickEntityAsync(Cgm.Core.Model.EntityCategory.Map, "Warp target map:") is { } target:
                doc.ConfigureEntity(warp with { Target = target });
                break;
        }
        Redraw();
    }
}

/// <summary>The entity kinds placeable from the map canvas (object placement needs an object pick,
/// handled separately).</summary>
public enum EntityKind { PlayerStart, Npc, Warp, Pickup, Sign, Trigger }
