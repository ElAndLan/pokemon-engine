using Avalonia.Controls;
using Avalonia.Interactivity;
using Cgm.Core.Model;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Views;

/// <summary>Hosts the shared reference picker as a modal choice; returns the picked EntityId or
/// null. Behavior lives in <see cref="ReferencePickerViewModel"/> — this is binding glue only.</summary>
public partial class PickEntityWindow : Window
{
    public PickEntityWindow() => InitializeComponent();

    public PickEntityWindow(ReferencePickerViewModel picker, string prompt) : this()
    {
        DataContext = picker;
        PromptText.Text = prompt;
    }

    private void OnOk(object? sender, RoutedEventArgs e) =>
        Close((DataContext as ReferencePickerViewModel)?.Selected?.Id);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
