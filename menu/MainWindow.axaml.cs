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
    private Lazy<TextEditor> _editor;
    private RegistryOptions? _registryOptions;
    private TextMate.Installation? _textMateInstallation;
    private Lazy<Image> _image;

    public MainWindow()
    {
        InitializeComponent();
        throw new InvalidOperationException("This constructor is for XAML runtime use only.");
    }
    public MainWindow(MainViewModel viewModel)
    {
        DataContext = viewModel;
        Topmost = true;
        _viewModel = viewModel;
        ShowInTaskbar = false;
        AdjustWindowSizeAndPosition();
        InitializeComponent();
        Loaded += OnLoaded;
        
        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;

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
        _editor = new Lazy<TextEditor>(InitTextEditorControl);
        _image = new Lazy<Image>(() => new Image());
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
    }
    
    private TextEditor InitTextEditorControl()
    {
        _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        var editor = new TextEditor();
        _textMateInstallation = editor.InstallTextMate(_registryOptions);
        editor.KeyUp += EditorOnKeyUp;
        if (this.TryFindResource("ForegroundBrush", null, out var resource) && resource is SolidColorBrush brush)
        {
            editor.Foreground = brush;
        }

        return editor;
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
                _editor.Value.Text = _viewModel.PreviewText;
                if (_editor == null || _textMateInstallation == null || _registryOptions == null)
                {
                    return;
                }

                if (_viewModel.PreviewExtension != null && _viewModel.PreviewExtension != ".txt")
                {
                    var languageByExtension = _registryOptions.GetLanguageByExtension(_viewModel.PreviewExtension);
                    if (languageByExtension != null)
                    {
                        var byLanguageId = _registryOptions.GetScopeByLanguageId(languageByExtension.Id);
                        _textMateInstallation.SetGrammar(byLanguageId);
                    }
                }

                PreviewContainer.Child = _editor.Value;
            });
        }

        if (e.PropertyName == "PreviewImage")
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_image != null)
                {
                    _image.Value.Source = _viewModel.PreviewImage;
                    PreviewContainer.Child = _image.Value;
                }
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
            if (_viewModel.HasPreview)
            {
                switch (e.Key)
                {
                    case Key.W:
                        _editor.Value.Focus();
                        return;
                    case Key.D:
                        _editor.Value.PageDown();
                        return;
                    case Key.U:
                        _editor.Value.PageUp();
                        return;
                }
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
