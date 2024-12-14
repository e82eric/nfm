using Avalonia;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using nfm.menu;

namespace nfm.Cli;

public class App<T> : Application where T:class
{
    private readonly MainViewModel<T> _viewModel;
    private readonly IMenuDefinitionProvider<T>? _definitionProvider;

    public App(MainViewModel<T> viewModel, IMenuDefinitionProvider<T> definitionProvider)
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
                await _viewModel.RunDefinitionAsync(definition);
                window.Show();
            });
        }
    }
}