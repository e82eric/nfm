using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace nfm.menu;

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
        AvaloniaXamlLoader.Load(this);
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