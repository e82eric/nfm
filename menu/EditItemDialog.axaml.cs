using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace nfm.menu;

    public partial class EditItemDialog : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly HighlightedText _current;

        public EditItemDialog(MainViewModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();
            NewTextBox.KeyUp += OnKeyUp;
            DataContext = viewModel;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            _current = viewModel.DisplayItems[viewModel.SelectedIndex];
            CurrentTextBlock.Text = _current.Text;
            NewTextBox.Text = _current.Text;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            NewTextBox.Focus();
            
            var left = (int)(Screens.Primary.Bounds.X + (Screens.Primary.Bounds.Width - this.Width) / 2);
            var top = Screens.Primary.Bounds.Y + (Screens.Primary.Bounds.Height) / 2;
            Position = new PixelPoint(left, top);
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _viewModel.EditDialogOpen = false;
                Close();
            }
            else if(e.Key == Key.Return)
            {
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    Close();
                    await _viewModel.RunEditAction(_current.BackingObj, NewTextBox.Text);
                    _viewModel.EditDialogOpen = false;
                });
            }
        }
    }
