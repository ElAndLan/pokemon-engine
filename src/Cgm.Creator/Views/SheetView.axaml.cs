using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
        if (Doc is { } doc && CellList.SelectedItem is SheetCellView cell)
            doc.RemoveCell(cell.SpriteId);
    }
}
