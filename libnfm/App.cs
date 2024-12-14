using Avalonia;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using nfzf.FileSystem;

namespace nfm.menu;

public class App : Application
{
    private readonly MainViewModel<FileSystemNode> _fileSystemViewModel;
    private readonly MainViewModel<string> _stringViewModel;

    public App(MainViewModel<string> stringViewModel, MainViewModel<FileSystemNode> fileSystemViewModel)
    {
        _fileSystemViewModel = fileSystemViewModel;
        _stringViewModel = stringViewModel;
    }

    public override void Initialize()
    {
        var fluentTheme = new FluentTheme
        {
            Resources = null,
            DensityStyle = DensityStyle.Normal
        };
        Styles.Add(fluentTheme);
        IsInitialized = true;
    }
    
    public bool IsInitialized { get; set; }

    public void RunDefinition(IMenuDefinitionProvider<string> definitionProvider)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var definition = definitionProvider.Get();
            var window = new MainWindow(_stringViewModel);
            await _stringViewModel.RunDefinitionAsync(definition);
            window.Show();
        });
    }
    public void RunDefinition(IMenuDefinitionProvider<FileSystemNode> definitionProvider)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var definition = definitionProvider.Get();
            var window = new MainWindow(_fileSystemViewModel);
            await _fileSystemViewModel.RunDefinitionAsync(definition);
            window.Show();
        });
    }
}