using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using nfm.menu;
using nfzf.FileSystem;

namespace tempA;

public partial class App(MainViewModel<FileSystemNode> viewModel, IMenuDefinitionProvider<FileSystemNode> command) : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(viewModel);
            viewModel.RunDefinitionAsync(command.Get());
        }

        base.OnFrameworkInitializationCompleted();
    }
}