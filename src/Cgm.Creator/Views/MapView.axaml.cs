using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
        DataContextChanged += (_, _) => Rebuild();
        DetachedFromVisualTree += (_, _) => _bitmaps?.Dispose();
        ApplyZoom();
    }

    private MapDocument? Doc => DataContext as MapDocument;

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
        foreach (PaletteTile tile in doc.PaletteTiles)
        {
            var image = new Image { Width = 32, Height = 32, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
            if (tile.Sprite is { } sprite)
                image.Source = _bitmaps?.Crop(sprite);

            var border = new Border
            {
                Width = 40, Height = 40, Margin = new(2),
                BorderBrush = tile.Index == doc.SelectedTile ? Brushes.DodgerBlue : Brushes.Transparent,
                BorderThickness = new(2),
                Child = image,
            };
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
        }
        MapImage.Source = target;
    }

    private void DrawLayer(DrawingContext ctx, MapDocument doc, MapLayerId layer, bool visible, int ts)
    {
        if (!visible)
            return;
        for (int y = 0; y < doc.Height; y++)
            for (int x = 0; x < doc.Width; x++)
            {
                int index = doc.TileAt(layer, x, y);
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

    // --- Pointer strokes ---

    private (int X, int Y)? CellAt(PointerEventArgs e)
    {
        if (Doc is not { } doc)
            return null;
        Point p = e.GetPosition(MapImage);
        int x = (int)(p.X / doc.TileSize), y = (int)(p.Y / doc.TileSize);
        return x >= 0 && y >= 0 && x < doc.Width && y < doc.Height ? (x, y) : null;
    }

    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Doc is not { } doc || CellAt(e) is not { } cell)
            return;

        if (CollisionToggle.IsChecked == true)
        {
            doc.SetCollision(cell.X, cell.Y, doc.CollisionAt(cell.X, cell.Y) is null ? doc.SelectedCollision : null);
            Redraw();
            return;
        }
        if (EncounterToggle.IsChecked == true)
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
        if (!_stroking || Doc is not { } doc || CellAt(e) is not { } cell)
            return;
        // Rect fill previews from the anchor; the other tools accumulate along the drag.
        if (doc.Tool == MapTool.RectFill)
        {
            doc.EndStroke();       // discard the interim preview
            doc.BeginStroke();
            doc.StrokePaint(cell.X, cell.Y, _anchor.X, _anchor.Y);
        }
        else
        {
            doc.StrokePaint(cell.X, cell.Y, _anchor.X, _anchor.Y);
        }
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

    private void OnZoomChanged(object? sender, RoutedEventArgs e) => ApplyZoom();

    private void ApplyZoom()
    {
        double zoom = ZoomSlider?.Value ?? 2;
        ZoomHost.LayoutTransform = new ScaleTransform(zoom, zoom);
    }

    private void OnOverlayToggled(object? sender, RoutedEventArgs e) => Redraw();

    private void OnRedraw(object? sender, RoutedEventArgs e) => Redraw();
}
