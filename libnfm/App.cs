using Avalonia;
using Avalonia.Themes.Simple;
using Avalonia.Threading;

namespace nfm.menu;

public class App : Application
{
    private readonly MainViewModel _viewModel;
    private MainWindow? _mainWindow;

    public App(MainViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public override void Initialize()
    {
        var theme = new SimpleTheme() { };
        Styles.Add(theme);
        Styles.Resources.Add("ContentControlThemeFontFamily", new Avalonia.Media.FontFamily("Segoe UI"));
        Styles.Resources.Add("ControlContentThemeFontSize", 14.0);
        IsInitialized = true;
        _mainWindow = new MainWindow(_viewModel);
    }
    
    public bool IsInitialized { get; set; }

    public void RunDefinition(IMenuDefinitionProvider definitionProvider)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (_mainWindow != null)
            {
                var definition = definitionProvider.Get();
                _mainWindow.Show();
                await _viewModel.RunDefinitionAsync(definition);
            }
        });
    }
    
    public void RunLastDefinition()
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await _viewModel.RunLastDefinition();
        });
    }
}