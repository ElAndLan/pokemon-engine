using Avalonia.Controls;
using Cgm.Core.Validation;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    // These bridge control selection to the view-model (UI glue only; no logic here).
    private void OnNavSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            sender is TreeView { SelectedItem: NavEntity entity })
        {
            vm.OpenDocument(entity.Id);
        }
    }

    private void OnIssueSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            sender is ListBox { SelectedItem: ValidationIssue issue })
        {
            vm.NavigateToIssue(issue);
        }
    }
}
