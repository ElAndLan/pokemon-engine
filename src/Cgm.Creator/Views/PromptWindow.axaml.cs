using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Cgm.Creator.Views;

public partial class PromptWindow : Window
{
    public PromptWindow() => InitializeComponent();

    public PromptWindow(string prompt, string initial) : this()
    {
        PromptText.Text = prompt;
        InputBox.Text = initial;
        Opened += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(InputBox.Text ?? string.Empty);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
