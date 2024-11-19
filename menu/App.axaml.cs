using Avalonia;
using Avalonia.Markup.Xaml;

namespace nfm.menu;

public class App : Application
{
    private readonly string _command;
    private readonly MainViewModel _viewModel;

    public App(string command, MainViewModel viewModel)
    {
        _command = command;
        _viewModel = viewModel;
    }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void Show()
    {
        var window  = new MainWindow(_viewModel);
        _viewModel.StartRead(_command, 50);
        window.Show();
    }
    
    public void ShowListWindows()
    {
        var window  = new MainWindow(_viewModel);
        _viewModel.ReadEnumerable(ListWindows.Run(), 0);
        window.Show();
    }
}