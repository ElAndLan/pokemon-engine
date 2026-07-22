using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Cgm.Creator.Services;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var dialogs = new AvaloniaDialogService(() => window);
            var viewModel = new MainWindowViewModel(dialogs);
            window.DataContext = viewModel;
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => viewModel.Session?.Close(); // clean close releases the lock (§10.3)
        }

        base.OnFrameworkInitializationCompleted();
    }
}
