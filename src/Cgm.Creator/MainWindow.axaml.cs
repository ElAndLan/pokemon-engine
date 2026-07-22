using Avalonia.Controls;
using Cgm.Core.Validation;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator;

public partial class MainWindow : Window
{
    private bool _closeApproved;

    public MainWindow()
    {
        InitializeComponent();
        // The §10.5 unsaved guard on app exit: cancel the close, ask, then re-close if approved.
        Closing += async (_, e) =>
        {
            if (_closeApproved || DataContext is not MainWindowViewModel vm)
                return;
            e.Cancel = true;
            if (await vm.ConfirmLoseChangesAsync())
            {
                _closeApproved = true;
                Close();
            }
        };
    }

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
