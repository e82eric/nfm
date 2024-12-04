using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace nfm.menu;

public class App : Application
{
    private readonly MainViewModel _viewModel;

    public App(MainViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        IsInitialized = true;
    }
    
    public bool IsInitialized { get; set; }

    public void RunDefinition(IMenuDefinitionProvider definitionProvider)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var definition = definitionProvider.Get();
            var window = new MainWindow(_viewModel);
            await _viewModel.RunDefinitionAsync(definition);
            window.Show();
        });
    }
}