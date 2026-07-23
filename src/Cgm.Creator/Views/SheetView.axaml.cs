using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cgm.Creator.ViewModels;
using Rect = Avalonia.Rect;

namespace Cgm.Creator.Views;

/// <summary>Slicer canvas glue: composites the sheet pixels with its cell-rect overlay into one
/// bitmap (redrawn on edits/selection), supports fractional zoom + fit-to-window for sheets of any
/// size, and forwards pointer gestures to the document. All authoring behaviour lives in
/// <see cref="SheetDocument"/>.</summary>
public partial class SheetView : UserControl
{
    private Bitmap? _sheet;
    private readonly HashSet<Cgm.Core.Model.EntityId> _selected = [];
    private bool _syncingSelection;

    public SheetView()
    {
        InitializeComponent();
        RenderOptions.SetBitmapInterpolationMode(SheetImage, BitmapInterpolationMode.None);
        DataContextChanged += (_, _) => Reload();
        DetachedFromVisualTree += (_, _) => _sheet?.Dispose();
        ApplyZoom();
    }

    private SheetDocument? Doc => DataContext as SheetDocument;

    private void Reload()
    {
        _sheet?.Dispose();
        _sheet = null;
        _selected.Clear();
        if (Doc is { } doc)
        {
            try { _sheet = new Bitmap(System.IO.Path.Combine(doc.Session.Folder, doc.AssetPath)); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { _sheet = null; }
        }
        Redraw();
        Dispatcher.UIThread.Post(FitToWindow, DispatcherPriority.Loaded);
    }

    /// <summary>Composites the sheet plus a cell-rect outline per slice (selected cells filled) into
    /// one native-resolution bitmap. One image means the overlay can never drift from the pixels,
    /// and it scales to any sheet size without a control-per-cell.</summary>
    private void Redraw()
    {
        if (Doc is not { } doc || _sheet is null)
        {
            SheetImage.Source = _sheet;
            InfoLabel.Text = _sheet is null ? "(sheet image missing)" : "";
            return;
        }

        int w = _sheet.PixelSize.Width, h = _sheet.PixelSize.Height;
        var target = new RenderTargetBitmap(new PixelSize(w, h));
        using (DrawingContext ctx = target.CreateDrawingContext())
        {
            ctx.DrawImage(_sheet, new Rect(0, 0, w, h));

            var outline = new Pen(new SolidColorBrush(Color.FromArgb(0xC0, 0xFF, 0x40, 0x40)));
            var pick = new Pen(new SolidColorBrush(Color.FromArgb(0xF0, 0x50, 0xC0, 0xFF)), 2);
            var pickFill = new SolidColorBrush(Color.FromArgb(0x40, 0x50, 0xC0, 0xFF));
            foreach (SheetCellView cell in doc.CellViews)
            {
                var r = new Rect(cell.Rect.X, cell.Rect.Y, cell.Rect.W, cell.Rect.H);
                bool selected = _selected.Contains(cell.SpriteId);
                if (selected)
                    ctx.FillRectangle(pickFill, r);
                ctx.DrawRectangle(null, selected ? pick : outline, r);
            }
        }
        SheetImage.Source = target;
        InfoLabel.Text = $"{w} × {h} px · {doc.CellViews.Count} sprites"
            + (doc.HasPixels ? "" : " · image not decoded");
    }

    // --- Zoom ---

    private void OnZoomChanged(object? sender, RoutedEventArgs e) => ApplyZoom();

    private void ApplyZoom()
    {
        double zoom = ZoomSlider?.Value ?? 2;
        ZoomHost.LayoutTransform = new ScaleTransform(zoom, zoom);
        if (ZoomLabel is not null)
            ZoomLabel.Text = $"{zoom * 100:0}%";
    }

    private void OnActualSize(object? sender, RoutedEventArgs e) => ZoomSlider.Value = 1;

    private void OnFit(object? sender, RoutedEventArgs e) => FitToWindow();

    /// <summary>Picks the largest zoom that fits the whole sheet in the viewport — the key to
    /// working with very long, wide, or large sheets.</summary>
    private void FitToWindow()
    {
        if (_sheet is null || CanvasScroll.Bounds.Width < 2 || CanvasScroll.Bounds.Height < 2)
            return;
        double fit = Math.Min(
            CanvasScroll.Bounds.Width / _sheet.PixelSize.Width,
            CanvasScroll.Bounds.Height / _sheet.PixelSize.Height);
        ZoomSlider.Value = Math.Clamp(fit, ZoomSlider.Minimum, ZoomSlider.Maximum);
    }

    // --- Slicing suggestions ---

    private void OnApplyGutter(object? sender, RoutedEventArgs e) { Doc?.ApplyGutterSuggestion(); Redraw(); }
    private void OnApplySize(object? sender, RoutedEventArgs e) { Doc?.ApplySizeSuggestion(); Redraw(); }
    private void OnApplyComponents(object? sender, RoutedEventArgs e) { Doc?.ApplyComponentSuggestion(); Redraw(); }

    private void OnRenameAll(object? sender, RoutedEventArgs e)
    {
        if (Doc is { } doc && !string.IsNullOrWhiteSpace(RenameBox.Text))
        {
            doc.RenameCells(RenameBox.Text);
            Redraw();
        }
    }

    private void OnRemoveSelected(object? sender, RoutedEventArgs e)
    {
        if (Doc is not { } doc)
            return;
        foreach (SheetCellView cell in CellList.SelectedItems?.OfType<SheetCellView>().ToList() ?? [])
            doc.RemoveCell(cell.SpriteId);
        _selected.Clear();
        Redraw();
    }

    private MainWindowViewModel? Shell => this.FindAncestorOfType<Window>()?.DataContext as MainWindowViewModel;

    private async void OnCreateAnimation(object? sender, RoutedEventArgs e)
    {
        var sprites = CellList.SelectedItems?.OfType<SheetCellView>().Select(c => c.SpriteId).ToList();
        if (Shell is { } shell && sprites is { Count: > 0 })
            await shell.CreateAnimationAsync(sprites);
    }

    private void OnCreateWalkClips(object? sender, RoutedEventArgs e)
    {
        if (Shell is { } shell && Doc?.Id is { } id)
            shell.CreateWalkClips(id);
    }

    private void OnCreateTileset(object? sender, RoutedEventArgs e)
    {
        if (Shell is { } shell && Doc?.Id is { } id)
            shell.CreateTilesetFromSheet(id);
    }

    private void OnRepairDimensions(object? sender, RoutedEventArgs e)
    {
        if (Doc is not { } doc)
            return;
        doc.ResliceToActualImage();
        Redraw();
    }

    // --- Selection (list <-> canvas kept in sync) ---

    private void OnListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection)
            return;
        _selected.Clear();
        foreach (SheetCellView cell in CellList.SelectedItems?.OfType<SheetCellView>() ?? [])
            _selected.Add(cell.SpriteId);
        Redraw();
    }

    private void SelectOnly(SheetCellView cell)
    {
        _selected.Clear();
        _selected.Add(cell.SpriteId);
        _syncingSelection = true;
        CellList.SelectedItems?.Clear();
        CellList.SelectedItems?.Add(cell);
        _syncingSelection = false;
        Redraw();
    }

    // --- Canvas pointer: click selects a cell; drag rubber-bands a new rect or moves a cell.
    // One gesture = one undo step, applied on release. Coordinates are native sheet pixels (the
    // Image lives inside the zoom transform, so GetPosition already de-scales).

    private Point _dragStart;
    private SheetCellView? _dragCell;
    private bool _dragging;

    private SheetCellView? CellUnder(Point p) => Doc?.CellViews.LastOrDefault(c =>
        p.X >= c.Rect.X && p.X < c.Rect.X + c.Rect.W && p.Y >= c.Rect.Y && p.Y < c.Rect.Y + c.Rect.H);

    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Doc is null)
            return;
        _dragStart = e.GetPosition(SheetImage);
        _dragCell = CellUnder(_dragStart);
        _dragging = true;
        e.Pointer.Capture(CanvasPanel);
    }

    private void OnCanvasMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || _dragCell is not null)
            return; // moving an existing cell shows no band; the drop applies it
        Point now = e.GetPosition(SheetImage);
        Canvas.SetLeft(RubberBand, Math.Min(_dragStart.X, now.X));
        Canvas.SetTop(RubberBand, Math.Min(_dragStart.Y, now.Y));
        RubberBand.Width = Math.Abs(now.X - _dragStart.X);
        RubberBand.Height = Math.Abs(now.Y - _dragStart.Y);
        RubberBand.IsVisible = true;
    }

    private void OnCanvasReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging || Doc is not { } doc)
            return;
        _dragging = false;
        RubberBand.IsVisible = false;
        e.Pointer.Capture(null);

        Point end = e.GetPosition(SheetImage);
        double moved = Math.Abs(end.X - _dragStart.X) + Math.Abs(end.Y - _dragStart.Y);

        // A near-stationary press is a click: select the cell under it (or clear).
        if (moved < 3)
        {
            if (CellUnder(_dragStart) is { } hit)
                SelectOnly(hit);
            else { _selected.Clear(); _syncingSelection = true; CellList.SelectedItems?.Clear(); _syncingSelection = false; Redraw(); }
            return;
        }

        if (_dragCell is { } cell)
        {
            int dx = (int)Math.Round(end.X - _dragStart.X);
            int dy = (int)Math.Round(end.Y - _dragStart.Y);
            if (dx != 0 || dy != 0)
                doc.SetCellRect(cell.SpriteId, cell.Rect with { X = cell.Rect.X + dx, Y = cell.Rect.Y + dy });
            Redraw();
            return;
        }

        var rect = new Cgm.Core.Model.Rect(
            (int)Math.Round(Math.Min(_dragStart.X, end.X)),
            (int)Math.Round(Math.Min(_dragStart.Y, end.Y)),
            (int)Math.Round(Math.Abs(end.X - _dragStart.X)),
            (int)Math.Round(Math.Abs(end.Y - _dragStart.Y)));
        if (rect.W >= 2 && rect.H >= 2) // a click is not a rect
            doc.AddRect(rect);
        Redraw();
    }
}
