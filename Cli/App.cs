using Avalonia;
using Avalonia.Themes.Simple;
using nfm.menu;

namespace nfm.Cli;

public class App : Application
{
    private readonly MainViewModel _viewModel;
    private readonly Func<IMenuDefinitionProvider> _definitionProvider;

    public App(MainViewModel viewModel, Func<IMenuDefinitionProvider> definitionProvider)
    {
        _viewModel = viewModel;
        _definitionProvider = definitionProvider;
    }

    public override void Initialize()
    {
        var theme = new SimpleTheme() { };
        Styles.Add(theme);
        Styles.Resources.Add("ContentControlThemeFontFamily", new Avalonia.Media.FontFamily("Segoe UI"));
        Styles.Resources.Add("ControlContentThemeFontSize", 14.0);
        var window = new MainWindow(_viewModel);
        window.Show();
        Task.Run(async () =>
        {
            var definition = _definitionProvider().Get();
            await _viewModel.RunDefinitionAsync(definition);
        });
    }
}