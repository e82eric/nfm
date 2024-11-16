using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace nfm.menu;

public partial class App : Application
{
    private readonly MainViewModel _viewModel = new MainViewModel();
    
    public App()
    {
        _viewModel.StartRead();
    }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new nfm.menu.MainWindow(_viewModel);
            desktop.MainWindow.DataContext = _viewModel;
        }

        base.OnFrameworkInitializationCompleted();
    }
}