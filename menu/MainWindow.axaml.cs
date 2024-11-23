using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace nfm.menu;
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ListBox? _listBox;

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

        ListBoxContainer.IsVisible = false;
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
    
    private void AdjustWindowSizeAndPosition()
    {
        // Get the primary screen information
        var screens = Screens.Primary;
        var screen = screens ?? Screens.All[0]; // Fallback in case Primary is null

        // Calculate 15% of the screen height
        var marginPercentage = 0.10;
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
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        TextBox.Focus();
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "ShowResults")
        {
            if (_viewModel.ShowResults)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    ListBoxContainer.IsVisible = true;
                });
            }
            else
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    this.
                        ListBoxContainer.IsVisible = false;
                });
            }
        }
        if (e.PropertyName == "IsVisible")
        {
            if (!_viewModel.IsVisible)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    ListBoxContainer.IsVisible = false;
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
            if (_viewModel.DisplayItems.Count > _viewModel.SelectedIndex)
            {
                var selected = _viewModel.DisplayItems[_viewModel.SelectedIndex];
                if (selected.Text.EndsWith(".mp4") || selected.Text.EndsWith(".wmv"))
                {
                    string arguments =
                        $"-ss 00:00:15 -i \"{selected.Text}\" -frames:v 1 -f image2pipe -vcodec png pipe:1";

                    Task.Run(() =>
                    {
                        try
                        {
                            var process = new Process
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    FileName = @"C:\msys64\mingw64\bin\ffmpeg.exe",
                                    Arguments = arguments,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    UseShellExecute = false,
                                    CreateNoWindow = true,
                                    StandardOutputEncoding = null,
                                }
                            };

                            process.Start();

                            using (var memoryStream = new MemoryStream())
                            {
                                process.StandardOutput.BaseStream.CopyTo(memoryStream);
                                process.WaitForExit();

                                string error = process.StandardError.ReadToEnd();
                                if (process.ExitCode != 0)
                                {
                                    //await MessageBox.Show(this, error, "FFmpeg Error", MessageBoxButtons.Ok);
                                    return;
                                }

                                memoryStream.Seek(0, SeekOrigin.Begin);
                                Dispatcher.UIThread.Invoke(() => { VideoFrameImage.Source = new Bitmap(memoryStream); });
                            }
                        }
                        catch (Exception ex)
                        {
                            //await MessageBox.Show(this, ex.Message, "Exception", MessageBoxButtons.Ok);
                        }
                    });
                }
            }

            Dispatcher.UIThread.Invoke(() =>
            {
                ListBox.SelectedIndex = _viewModel.SelectedIndex;
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

    private async void TextBoxOnKeyUp(object? sender, KeyEventArgs e)
    {
        await _viewModel.HandleKey(e.Key, e.KeyModifiers);
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