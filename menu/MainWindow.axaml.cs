using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Timers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
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
            textBox.KeyDown += TextBoxOnKeyUp;
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

    
    private void AdjustWindowSizeAndPosition()
    {
        var margin = .3;
        if (_viewModel.HasPreview)
        {
            margin = .1;
        }
        var screens = Screens.Primary;
        var screen = screens ?? Screens.All[0]; // Fallback in case Primary is null

        // Calculate 15% of the screen height
        var marginPercentage = margin;
        var topBottomMargin = screen.Bounds.Height * marginPercentage;

        // Calculate the desired window height (70% of the screen height)
        var windowHeight = screen.Bounds.Height - (2 * topBottomMargin);

        // Set the window height
        this.Height = windowHeight;

        // Optionally, set the window width to match the screen width or any desired value
        // For example, set width to 80% of screen width
        var windowWidth = screen.Bounds.Width * 0.5;
        this.Width = windowWidth;

        // Calculate the position to center the window horizontally and apply top margin
        var left = screen.Bounds.X + (screen.Bounds.Width - this.Width) / 2;
        var top = screen.Bounds.Y + topBottomMargin;

        // Set the window position
        this.Position = new PixelPoint((int)left, (int)top);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        //_viewModel.Close();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        BringToForeground();
        TextBox.Focus();
        
        _editor = new TextEditor
        {
            IsReadOnly = true,
        };
        _editor.KeyUp += EditorOnKeyUp;
        if (Resources.TryGetResource("ForegroundBrush", null, out var resource) && resource is SolidColorBrush brush)
        {
            _editor.Foreground = brush;
        }
        _registryOptions = new RegistryOptions(ThemeName.DarkPlus);

        _textMateInstallation = _editor.InstallTextMate(_registryOptions);
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        TextBox.Focus();
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "HasPreview")
        {
            AdjustWindowSizeAndPosition();
        }
        
        if (e.PropertyName == "IsVisible")
        {
            if (!_viewModel.IsVisible)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    //ListBoxContainer.IsVisible = false;
                    Close();
                });
            }
            else
            {
                BringToForeground();
                Dispatcher.UIThread.Invoke(() =>
                {
                    TextBox.Focus();
                });
            }
        }

        if (e.PropertyName == "SelectedIndex")
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                ListBox.SelectedIndex = _viewModel.SelectedIndex;
            });
        }

        if (e.PropertyName == "PreviewText")
        {
            Dispatcher.UIThread.Post(() =>
            {
                _editor.Text = _viewModel.PreviewText;
                if (_viewModel.PreviewExtension != ".txt")
                {
                    var languageByExtension = _registryOptions.GetLanguageByExtension(_viewModel.PreviewExtension);
                    if (languageByExtension != null)
                    {
                        var byLanguageId = _registryOptions.GetScopeByLanguageId(languageByExtension.Id);
                        _textMateInstallation.SetGrammar( byLanguageId);
                    }
                }

                PreviewContainer.Child = _editor;
            });
        }

        if (e.PropertyName == "PreviewImage")
        {
            var image = new Image();
            image.Source = _viewModel.PreviewImage;
            Dispatcher.UIThread.Invoke(() =>
            {
                PreviewContainer.Child = image;
            });
        }

        if (e.PropertyName == "DisplayItems")
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                ListBox.Items.Clear();
                foreach (var displayItem in _viewModel.DisplayItems)
                {
                    ListBox.Items.Add(displayItem);
                }

                if (_viewModel.SelectedIndex >= 0 && _viewModel.SelectedIndex <= ListBox.Items.Count - 1)
                {
                    ListBox.SelectedIndex = _viewModel.SelectedIndex;
                }
            });
        }
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

    private async void TextBoxOnKeyUp(object? sender, KeyEventArgs e)
    {
        await _viewModel.HandleKey(e.Key, e.KeyModifiers);

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
    
    private void FocusWindowWithWin32()
    {
        var handle = TryGetPlatformHandle();
        IntPtr? hwnd = handle?.Handle;

        if (hwnd != null)
        {
            keybd_event(0x12, 0, 0, UIntPtr.Zero);
            keybd_event(0x12, 0, 0x0002, UIntPtr.Zero);
            SetForegroundWindow(hwnd.Value);
        }
    }

    private void BringToForeground()
    {
        FocusWindowWithWin32();
        INPUT input = new INPUT { Type = INPUTTYPE.INPUTMOUSE, Data = { } };
        INPUT[] inputs = new INPUT[] { input };

        _ = SendInput(1, inputs, INPUT.Size);
    }
    
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    
    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public INPUTTYPE Type;
        public InputUnion Data;

        public static int Size
        {
            get { return Marshal.SizeOf(typeof(INPUT)); }
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Matching COM")]
    internal struct InputUnion
    {
        [FieldOffset(0)]
        internal MOUSEINPUT mi;
        [FieldOffset(0)]
        internal KEYBDINPUT ki;
        [FieldOffset(0)]
        internal HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Matching COM")]
    internal struct MOUSEINPUT
    {
        internal int dx;
        internal int dy;
        internal int mouseData;
        internal uint dwFlags;
        internal uint time;
        internal UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Matching COM")]
    internal struct KEYBDINPUT
    {
        internal short wVk;
        internal short wScan;
        internal uint dwFlags;
        internal int time;
        internal UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Matching COM")]
    internal struct HARDWAREINPUT
    {
        internal int uMsg;
        internal short wParamL;
        internal short wParamH;
    }

    internal enum INPUTTYPE : uint
    {
        INPUTMOUSE = 0,
    }
    
    [DllImport("user32.dll")]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}