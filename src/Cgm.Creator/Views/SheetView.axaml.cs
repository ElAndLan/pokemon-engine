using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Views;

/// <summary>Slicer canvas glue: loads the sheet bitmap, applies integer zoom (nearest-neighbor),
/// and forwards button clicks to the document. All authoring behavior lives in
/// <see cref="SheetDocument"/>.</summary>
public partial class SheetView : UserControl
{
    public SheetView()
    {
        InitializeComponent();
        RenderOptions.SetBitmapInterpolationMode(SheetImage, BitmapInterpolationMode.None);
        DataContextChanged += (_, _) => LoadBitmap();
        ApplyZoom();
    }

    private SheetDocument? Doc => DataContext as SheetDocument;

    private void LoadBitmap()
    {
        if (Doc is not { } doc)
            return;
        try
        {
            SheetImage.Source = new Bitmap(System.IO.Path.Combine(doc.Session.Folder, doc.AssetPath));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SheetImage.Source = null; // validation reports the missing/corrupt asset
        }
    }

    private void OnZoomChanged(object? sender, RoutedEventArgs e) => ApplyZoom();

    private void ApplyZoom()
    {
        double zoom = ZoomSlider?.Value ?? 2;
        ZoomHost.LayoutTransform = new ScaleTransform(zoom, zoom);
    }

    private void OnApplyGutter(object? sender, RoutedEventArgs e) => Doc?.ApplyGutterSuggestion();

    private void OnApplySize(object? sender, RoutedEventArgs e) => Doc?.ApplySizeSuggestion();

    private void OnApplyComponents(object? sender, RoutedEventArgs e) => Doc?.ApplyComponentSuggestion();

    private void OnRenameAll(object? sender, RoutedEventArgs e)
    {
        if (Doc is { } doc && !string.IsNullOrWhiteSpace(RenameBox.Text))
            doc.RenameCells(RenameBox.Text);
    }

    private void OnRemoveSelected(object? sender, RoutedEventArgs e)
    {
        if (Doc is not { } doc)
            return;
        foreach (SheetCellView cell in CellList.SelectedItems?.OfType<SheetCellView>().ToList() ?? [])
            doc.RemoveCell(cell.SpriteId);
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

    // --- Canvas drag (ASSET_PIPELINE_SPEC 17B canvas semantics): drag on empty space rubber-bands
    // a new rect; drag on a cell moves it. One gesture = one undo step, applied on release.

    private Avalonia.Point _dragStart;
    private SheetCellView? _dragCell;
    private bool _dragging;

    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Doc is null)
            return;
        _dragStart = e.GetPosition(CanvasPanel);
        _dragCell = Doc.CellViews.LastOrDefault(c =>
            _dragStart.X >= c.Rect.X && _dragStart.X < c.Rect.X + c.Rect.W &&
            _dragStart.Y >= c.Rect.Y && _dragStart.Y < c.Rect.Y + c.Rect.H);
        _dragging = true;
        e.Pointer.Capture(CanvasPanel);
    }

    private void OnCanvasMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || _dragCell is not null)
            return; // moving a cell shows no band; the drop applies it
        Avalonia.Point now = e.GetPosition(CanvasPanel);
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

        Avalonia.Point end = e.GetPosition(CanvasPanel);
        if (_dragCell is { } cell)
        {
            int dx = (int)Math.Round(end.X - _dragStart.X);
            int dy = (int)Math.Round(end.Y - _dragStart.Y);
            if (dx != 0 || dy != 0)
                doc.SetCellRect(cell.SpriteId, cell.Rect with { X = cell.Rect.X + dx, Y = cell.Rect.Y + dy });
            return;
        }

        var rect = new Cgm.Core.Model.Rect(
            (int)Math.Round(Math.Min(_dragStart.X, end.X)),
            (int)Math.Round(Math.Min(_dragStart.Y, end.Y)),
            (int)Math.Round(Math.Abs(end.X - _dragStart.X)),
            (int)Math.Round(Math.Abs(end.Y - _dragStart.Y)));
        if (rect.W >= 2 && rect.H >= 2) // a click is not a rect
            doc.AddRect(rect);
    }
}
