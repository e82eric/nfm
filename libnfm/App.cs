using Avalonia;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using nfzf.FileSystem;

namespace nfm.menu;

public class App : Application
{
    private readonly MainViewModel _viewModel;
    private MainWindow _mainWindow;

    public App(MainViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public override void Initialize()
    {
        var fluentTheme = new FluentTheme { };
        Styles.Add(fluentTheme);
        IsInitialized = true;
        _mainWindow = new MainWindow(_viewModel);
    }
    
    public bool IsInitialized { get; set; }

    public void RunDefinition(IMenuDefinitionProvider definitionProvider)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var definition = definitionProvider.Get();
            _mainWindow.Show();
            await _viewModel.RunDefinitionAsync(definition);
        });
    }
}