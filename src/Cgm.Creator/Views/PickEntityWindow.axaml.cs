using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Cgm.Core.Model;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Views;

/// <summary>Hosts the shared reference picker as a modal choice; returns the picked EntityId or
/// null. When a <see cref="SpriteBitmaps"/> is supplied (picking a sprite), each row shows the
/// sprite's thumbnail so you can pick by sight, not by slug. Behaviour lives in
/// <see cref="ReferencePickerViewModel"/>.</summary>
public partial class PickEntityWindow : Window
{
    private readonly SpriteBitmaps? _thumbnails;

    public PickEntityWindow() => InitializeComponent();

    public PickEntityWindow(ReferencePickerViewModel picker, string prompt, SpriteBitmaps? thumbnails = null) : this()
    {
        DataContext = picker;
        PromptText.Text = prompt;
        _thumbnails = thumbnails;

        if (thumbnails is not null)
            ChoiceList.ItemTemplate = new FuncDataTemplate<ReferenceChoice>((choice, _) =>
            {
                var thumb = new Image { Width = 40, Height = 40, Stretch = Stretch.Uniform };
                RenderOptions.SetBitmapInterpolationMode(thumb, BitmapInterpolationMode.None);
                if (choice is not null)
                    thumb.Source = thumbnails.Crop(choice.Id);
                return new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children =
                    {
                        thumb,
                        new TextBlock { Text = choice?.Display, VerticalAlignment = VerticalAlignment.Center },
                    },
                };
            });

        Opened += (_, _) => SearchBox.Focus();
        Closed += (_, _) => _thumbnails?.Dispose();
    }

    private void OnOk(object? sender, RoutedEventArgs e) =>
        Close((DataContext as ReferencePickerViewModel)?.Selected?.Id);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
