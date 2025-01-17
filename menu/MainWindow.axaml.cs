using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace nfm.menu;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ListBox? _listBox;
    private TextEditor _editor;
    private RegistryOptions _registryOptions;
    private TextMate.Installation? _textMateInstallation;
    private Image _image;

    public MainWindow(MainViewModel viewModel)
    {
        DataContext = viewModel;
        Topmost = true;
        _viewModel = viewModel;
        ShowInTaskbar = false;
        InitializeComponent();
        AdjustWindowSizeAndPosition();
        Loaded += OnLoaded;
        
        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;

        ListBoxContainer.IsVisible = true;
        _listBox = this.FindControl<ListBox>("ListBox");
        var textBox = this.FindControl<TextBox>("TextBox");
        if (_listBox != null)
        {
            _listBox.GotFocus += ListBox_GotFocus;
        }
        if (textBox != null)
        {
            textBox.KeyDown += TextBoxOnKeyDown;
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

    private async void TextBoxOnKeyUp(object? sender, KeyEventArgs e)
    {
        await _viewModel.HandleKeyUp(e.Key, e.KeyModifiers);
    }

    private void AdjustWindowSizeAndPosition()
    {
        var margin = .3;
        if (_viewModel.HasPreview)
        {
            margin = .1;
        }
        var screens = Screens.Primary;
        var screen = screens ?? Screens.All[0];

        var marginPercentage = margin;
        var topBottomMargin = screen.Bounds.Height * marginPercentage;

        var windowHeight = screen.Bounds.Height - (2 * topBottomMargin);

        Height = windowHeight;

        var windowWidth = screen.Bounds.Width * 0.5;
        Width = windowWidth;

        var left = screen.Bounds.X + (screen.Bounds.Width - this.Width) / 2;
        var top = screen.Bounds.Y + topBottomMargin;

        Position = new PixelPoint((int)left, (int)top);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        BringToForeground();
        TextBox.Focus();
        InitTextEditorControl();
        _image = new Image();
    }
    
    private void InitTextEditorControl()
    {
        _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        _editor = new TextEditor();
        _textMateInstallation = _editor.InstallTextMate(_registryOptions);
        _editor.KeyUp += EditorOnKeyUp;
        if (this.TryFindResource("ForegroundBrush", null, out var resource) && resource is SolidColorBrush brush)
        {
            _editor.Foreground = brush;
        }
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        TextBox.Focus();
    }
    
    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "EditDialogOpen")
        {
            if (_viewModel.EditDialogOpen)
            {
                var dialog = new EditItemDialog(_viewModel)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual
                };

                dialog.Position = new PixelPoint(
                    (int)(Bounds.X + (Bounds.Width / 2) - (dialog.Width / 2)),
                    (int)(Bounds.X + (Bounds.Height / 2) - (dialog.Width / 2))
                );
                
                dialog.ShowDialog(this);
            }
            else
            {
                BringToForeground();
            }
        }
        
        if (e.PropertyName == "HasPreview")
        {
            Dispatcher.UIThread.Post(() =>
            {
                AdjustWindowSizeAndPosition();
                ListBoxContainer.InvalidateArrange();
                Root.InvalidateArrange();
            });
        }
        
        if (e.PropertyName == "IsVisible")
        {
            if (!_viewModel.IsVisible)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    ListBox.IsVisible = false;
                    PreviewContainer.Child = null;
                });
                var timer = new System.Timers.Timer(100);
                timer.AutoReset = false;
                timer.Elapsed += (sender, args) =>
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        Hide();
                    });

                    timer.Dispose();
                };
                timer.Start();
            }
            else
            {
                BringToForeground();
                Dispatcher.UIThread.Invoke(() =>
                {
                    ListBox.IsVisible = true;
                    AdjustWindowSizeAndPosition();
                    Show();
                    TextBox.Focus();
                });
            }
        }

        if (e.PropertyName == "SelectedIndex")
        {
            Dispatcher.UIThread.Post(() =>
            {
                ListBox.SelectedIndex = _viewModel.SelectedIndex;
            });
        }

        if (e.PropertyName == "PreviewText")
        {
            Dispatcher.UIThread.Post(() =>
            {
                _editor.Text = _viewModel.PreviewText;

                if (_viewModel.PreviewExtension != null && _viewModel.PreviewExtension != ".txt")
                {
                    var languageByExtension = _registryOptions.GetLanguageByExtension(_viewModel.PreviewExtension);
                    if (languageByExtension != null)
                    {
                        var byLanguageId = _registryOptions.GetScopeByLanguageId(languageByExtension.Id);
                        _textMateInstallation.SetGrammar(byLanguageId);
                    }
                }

                PreviewContainer.Child = _editor;
            });
        }

        if (e.PropertyName == "PreviewImage")
        {
            Dispatcher.UIThread.Post(() =>
            {
                _image.Source = _viewModel.PreviewImage;
                PreviewContainer.Child = _image;
            });
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Closed();
        _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        base.OnClosed(e);
    }

    private void EditorOnKeyUp(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                TextBox.Focus();
                break;
        }
    }

    private void TextBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await _viewModel.HandleKey(e.Key, e.KeyModifiers);
        });

        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.W:
                    _editor.Focus();
                    break;
            }
        }
    }

    private void ListBox_GotFocus(object? sender, GotFocusEventArgs e)
    {
        TextBox?.Focus();
        e.Handled = true;
    }

    private void BringToForeground()
    {
        var platformHandle = TryGetPlatformHandle();
        if (platformHandle != null)
        {
            FocusStealer.BringToForeground(platformHandle);
        }
    }
}
