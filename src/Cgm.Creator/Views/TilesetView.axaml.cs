using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Cgm.Core.Model;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Views;

/// <summary>Tileset editor glue: renders the tile grid with sprite thumbnails and drives the
/// flag inspector for the selected tile. All edits go through <see cref="TilesetDocument"/>.</summary>
public partial class TilesetView : UserControl
{
    private SpriteBitmaps? _bitmaps;
    private TilesetDocument? _subscribed;
    private int _selected = -1;
    private bool _syncing; // suppresses inspector-control events while loading the selection

    public TilesetView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SubscribeAndRebuild();
        DetachedFromVisualTree += (_, _) => UnsubscribeAndDispose();
    }

    private TilesetDocument? Doc => DataContext as TilesetDocument;

    private void SubscribeAndRebuild()
    {
        if (_subscribed is not null)
            _subscribed.Undo.Changed -= Rebuild;
        _subscribed = Doc;
        if (_subscribed is not null)
            _subscribed.Undo.Changed += Rebuild;
        Rebuild();
    }

    private void UnsubscribeAndDispose()
    {
        if (_subscribed is not null)
            _subscribed.Undo.Changed -= Rebuild;
        _subscribed = null;
        _bitmaps?.Dispose();
    }

    private void Rebuild()
    {
        _bitmaps?.Dispose();
        _bitmaps = Doc is { } doc ? new SpriteBitmaps(doc.Session) : null;
        TileGrid.Children.Clear();
        if (Doc is not { } d)
            return;

        for (int i = 0; i < d.Tiles.Count; i++)
            TileGrid.Children.Add(TileCell(i, d.Tiles[i]));

        if (_selected >= d.Tiles.Count)
            _selected = -1;
        LoadInspector();
    }

    private Control TileCell(int index, Tile tile)
    {
        var image = new Image { Width = 48, Height = 48, Stretch = Stretch.Uniform, Margin = new(4) };
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
        if (tile.Sprite is { } sprite)
            image.Source = _bitmaps?.Crop(sprite);

        var border = new Border
        {
            Width = 64,
            Height = 80,
            Margin = new(2),
            BorderBrush = index == _selected ? Brushes.DodgerBlue : new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80)),
            BorderThickness = new(index == _selected ? 2 : 1),
            Child = new StackPanel
            {
                Children =
                {
                    image,
                    new TextBlock { Text = index.ToString(), HorizontalAlignment = HorizontalAlignment.Center, FontSize = 11 },
                    new TextBlock { Text = FlagString(tile), HorizontalAlignment = HorizontalAlignment.Center, FontSize = 10, Opacity = 0.7 },
                },
            },
        };
        int captured = index;
        border.PointerPressed += (_, _) => Select(captured);
        return border;
    }

    private static string FlagString(Tile t) => string.Concat(
        t.Solid ? "S" : "", t.Grass ? "G" : "", t.Water ? "W" : "",
        t.Counter ? "C" : "", t.Ledge != LedgeDir.None ? "L" : "");

    private void Select(int index)
    {
        _selected = index;
        Rebuild(); // repaint selection highlight
    }

    private void LoadInspector()
    {
        if (Doc is not { } doc || _selected < 0 || _selected >= doc.Tiles.Count)
        {
            Inspector.IsEnabled = false;
            TileHeader.Text = "No tile selected";
            return;
        }

        _syncing = true;
        Tile tile = doc.Tiles[_selected];
        Inspector.IsEnabled = true;
        TileHeader.Text = $"Tile {_selected}";
        SpriteLabel.Text = tile.Sprite?.ToString() ?? "(no sprite)";
        SolidBox.IsChecked = tile.Solid;
        GrassBox.IsChecked = tile.Grass;
        WaterBox.IsChecked = tile.Water;
        CounterBox.IsChecked = tile.Counter;
        LedgeBox.SelectedItem = tile.Ledge;
        TerrainBox.Text = tile.TerrainTag;
        _syncing = false;
    }

    private void OnTileSelected(object? sender, SelectionChangedEventArgs e) { }

    private void OnAddTile(object? sender, RoutedEventArgs e)
    {
        if (Doc is { } doc && !doc.AddTile() && Shell is { } shell)
            shell.StatusText = doc.CountChangeBlockReason(removing: false) ?? "The tile could not be added.";
    }

    private void OnRemoveLast(object? sender, RoutedEventArgs e)
    {
        if (Doc is { Tiles.Count: > 0 } doc)
        {
            if (!doc.RemoveTile(doc.Tiles.Count - 1) && Shell is { } shell)
                shell.StatusText = doc.CountChangeBlockReason(removing: true) ?? "The trailing tile could not be removed.";
        }
    }

    private void OnClearTile(object? sender, RoutedEventArgs e)
    {
        if (Doc is { } doc && _selected >= 0)
        {
            doc.ClearTile(_selected);
            Rebuild();
        }
    }

    private async void OnPickSprite(object? sender, RoutedEventArgs e)
    {
        if (Doc is not { } doc || _selected < 0 || Shell is not { } shell)
            return;
        var candidates = doc.AvailableSprites.Select(id => (id, id.Slug)).ToList();
        if (await shell.PickSpriteAsync(candidates) is { } sprite)
        {
            doc.SetSprite(_selected, sprite);
            Rebuild();
        }
    }

    private MainWindowViewModel? Shell => this.FindAncestorOfType<Window>()?.DataContext as MainWindowViewModel;

    private void OnSolid(object? sender, RoutedEventArgs e) => Flag(d => d.SetSolid(_selected, SolidBox.IsChecked == true));
    private void OnGrass(object? sender, RoutedEventArgs e) => Flag(d => d.SetGrass(_selected, GrassBox.IsChecked == true));
    private void OnWater(object? sender, RoutedEventArgs e) => Flag(d => d.SetWater(_selected, WaterBox.IsChecked == true));
    private void OnCounter(object? sender, RoutedEventArgs e) => Flag(d => d.SetCounter(_selected, CounterBox.IsChecked == true));

    private void OnLedge(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && Doc is { } doc && _selected >= 0 && LedgeBox.SelectedItem is LedgeDir dir)
        {
            doc.SetLedge(_selected, dir);
            RefreshCell();
        }
    }

    private void OnTerrain(object? sender, RoutedEventArgs e)
    {
        if (!_syncing && Doc is { } doc && _selected >= 0)
            doc.SetTerrainTag(_selected, TerrainBox.Text ?? "");
    }

    private void Flag(Action<TilesetDocument> edit)
    {
        if (!_syncing && Doc is { } doc && _selected >= 0)
        {
            edit(doc);
            RefreshCell();
        }
    }

    /// <summary>Repaints just the selected tile's cell so its flag summary updates without a full
    /// grid rebuild stealing focus from the inspector.</summary>
    private void RefreshCell()
    {
        if (Doc is { } doc && _selected >= 0 && _selected < TileGrid.Children.Count)
            TileGrid.Children[_selected] = TileCell(_selected, doc.Tiles[_selected]);
    }
}
