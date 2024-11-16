using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace nfm.menu;
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ListBox? _listBox;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        ShowInTaskbar = false;
        Opened += MainWindow_Opened;

        _listBox = this.FindControl<ListBox>("ListBox");
        var textBox = this.FindControl<TextBox>("TextBox");
        if (_listBox != null)
        {
            _listBox.GotFocus += ListBox_GotFocus;
        }
        if (textBox != null)
        {
            textBox.KeyUp += TextBoxOnKeyUp;
        }
        var screen = Screens.Primary;
        if (screen != null)
        {
            var workingArea = screen.WorkingArea;
            Position = new PixelPoint(
                (int)(workingArea.Width - Width) / 2 + workingArea.X,
                (int)(workingArea.Height - Height) / 2 + workingArea.Y
            );
        }
    }

    private void TextBoxOnKeyUp(object? sender, KeyEventArgs e)
    {
        _viewModel.HandleKey(e.Key);
    }

    private void ListBox_GotFocus(object? sender, GotFocusEventArgs e)
    {
        TextBox?.Focus();
        e.Handled = true;
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        TextBox.Focus();
    }
}