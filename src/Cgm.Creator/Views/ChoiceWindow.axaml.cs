using Avalonia.Controls;

namespace Cgm.Creator.Views;

/// <summary>A message with N buttons; returns the clicked button's index, or null when the window
/// is closed without choosing. The last button is the default (Enter).</summary>
public partial class ChoiceWindow : Window
{
    public ChoiceWindow() => InitializeComponent();

    public ChoiceWindow(string message, params string[] buttons) : this()
    {
        MessageText.Text = message;
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            var button = new Button { Content = buttons[i], MinWidth = 80, IsDefault = i == buttons.Length - 1 };
            button.Click += (_, _) => Close(index);
            ButtonRow.Children.Add(button);
        }
    }
}
