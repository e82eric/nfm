using System.Threading.Tasks;
using Avalonia;
using Avalonia.Markup.Xaml;

namespace nfm.menu;

public class KeyHandlerApp(MainViewModel viewModel) : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public async Task RunDefinition(MenuDefinition definition)
    {
        var window = new MainWindow(viewModel);
        await viewModel.RunDefinitionAsync(definition);
        window.Show();
    }
}