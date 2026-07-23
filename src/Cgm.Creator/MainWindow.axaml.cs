using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cgm.Core.Validation;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator;

public partial class MainWindow : Window
{
    private bool _closeApproved;

    public MainWindow()
    {
        InitializeComponent();

        // Ctrl+W closes the active document tab.
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.W && e.KeyModifiers == KeyModifiers.Control
                && DataContext is MainWindowViewModel { ActiveDocument: { } doc } vm)
            {
                vm.CloseDocument(doc);
                e.Handled = true;
            }
        };

        // Recovery snapshots (§10.4): a coarse tick drives the 120 s dirty-inactivity autosave;
        // deactivating the app while dirty snapshots immediately. Logic lives in the view-model.
        var autosave = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        autosave.Tick += (_, _) => (DataContext as MainWindowViewModel)?.AutosaveTick(DateTime.UtcNow);
        autosave.Start();
        Deactivated += (_, _) => (DataContext as MainWindowViewModel)?.SnapshotNow();
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
    // Selection just selects; double-tapping an entity opens it (matches the tree's hint), so a
    // single click can browse the list without spawning tabs.
    private void OnNavSelectionChanged(object? sender, SelectionChangedEventArgs e) { }

    private void OnNavDoubleTapped(object? sender, TappedEventArgs e)
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
