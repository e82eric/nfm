using Avalonia;
using Avalonia.Input;
using Avalonia.Themes.Fluent;
using nfzf.FileSystem;

namespace nfm.menu;

public class KeyHandlerApp(MainViewModel<string> stringViewModel) : Application
{
    public override void Initialize()
    {
        var fluentTheme = new FluentTheme { };
        Styles.Add(fluentTheme);
    }

    public async Task RunDefinition(MenuDefinition<FileSystemNode> definition)
    {
        var viewModel = new MainViewModel<FileSystemNode>();
        viewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
        var window = new MainWindow(viewModel);
        await viewModel.Clear();
        await viewModel.RunDefinitionAsync(definition);
        window.Show();
    }
    public async Task RunDefinition(MenuDefinition<string> definition)
    {
        var window = new MainWindow(stringViewModel);
        await stringViewModel.Clear();
        await stringViewModel.RunDefinitionAsync(definition);
        window.Show();
    }
}