using Avalonia;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using nfm.menu;

namespace nfm.Cli;

public class App : Application
{
    private readonly MainViewModel _viewModel;
    private readonly IMenuDefinitionProvider? _definitionProvider;

    public App(MainViewModel viewModel, IMenuDefinitionProvider definitionProvider)
    {
        _viewModel = viewModel;
        _definitionProvider = definitionProvider;
    }

    public override void Initialize()
    {
        var fluentTheme = new FluentTheme { };
        Styles.Add(fluentTheme);
        if (_definitionProvider != null)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var definition = _definitionProvider.Get();
                var window = new MainWindow(_viewModel);
                window.Show();
                await _viewModel.RunDefinitionAsync(definition);
            });
        }
    }
}