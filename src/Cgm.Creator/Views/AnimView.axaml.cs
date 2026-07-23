using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Cgm.Core.Model;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Views;

/// <summary>Animation editor glue: frame list operations forward to the document; the preview is
/// a fixed-tick timer stepping through the authored frame durations, cropping each sprite out of
/// its sheet bitmap. No authoring state lives here.</summary>
public partial class AnimView : UserControl
{
    private readonly DispatcherTimer _timer = new();
    private SpriteBitmaps? _bitmaps;
    private int _frame;

    public AnimView()
    {
        InitializeComponent();
        RenderOptions.SetBitmapInterpolationMode(PreviewImage, BitmapInterpolationMode.None);
        _timer.Tick += (_, _) => Advance();
        DataContextChanged += (_, _) =>
        {
            _bitmaps?.Dispose();
            _bitmaps = Doc is { } doc ? new SpriteBitmaps(doc.Session) : null;
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _timer.Stop();
            _bitmaps?.Dispose();
        };
    }

    private AnimDocument? Doc => DataContext as AnimDocument;

    private void OnPlayToggled(object? sender, RoutedEventArgs e)
    {
        if (PlayToggle.IsChecked == true && Doc is { Frames.Count: > 0 })
        {
            _frame = 0;
            ShowFrame();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void Advance()
    {
        if (Doc is not { Frames.Count: > 0 } doc)
        {
            _timer.Stop();
            return;
        }
        _frame++;
        if (_frame >= doc.Frames.Count)
        {
            if (!doc.Loop)
            {
                _timer.Stop();
                PlayToggle.IsChecked = false;
                return;
            }
            _frame = 0;
        }
        ShowFrame();
    }

    /// <summary>Shows the current frame and arms the timer with that frame's authored duration —
    /// the preview honors per-frame timing exactly.</summary>
    private void ShowFrame()
    {
        if (Doc is not { } doc || _frame >= doc.Frames.Count)
            return;
        AnimFrame frame = doc.Frames[_frame];
        PreviewImage.Source = _bitmaps?.Crop(frame.Sprite);
        PreviewLabel.Text = $"{frame.Sprite.Slug} · {frame.Ms} ms";
        _timer.Interval = TimeSpan.FromMilliseconds(frame.Ms);
        _timer.Start();
    }

    private int Selected => FrameList.SelectedIndex;

    private void OnMoveUp(object? sender, RoutedEventArgs e) => Doc?.MoveFrame(Selected, -1);

    private void OnMoveDown(object? sender, RoutedEventArgs e) => Doc?.MoveFrame(Selected, +1);

    private void OnRemove(object? sender, RoutedEventArgs e) => Doc?.RemoveFrame(Selected);

    private void OnSetMs(object? sender, RoutedEventArgs e)
    {
        if (Doc is { } doc && MsBox.Value is { } ms)
            doc.SetFrameMs(Selected, (int)ms);
    }
}
