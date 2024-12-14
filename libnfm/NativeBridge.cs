﻿using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using nfzf.FileSystem;

namespace nfm.menu;

public static class NativeBridge
{
    private static Thread? _appThread;
    private static MainViewModel<string>? _stringViewModel;
    private static MainViewModel<FileSystemNode>? _fileSystemViewModel;
    private static IMainViewModel? _currentViewModel;
    private static App? _app;
    
    private unsafe class ListWindowsNativeResultHandler(delegate* unmanaged<IntPtr, void*, void> onSelect, void* state) : IResultHandler<string>
    {
        public Task HandleAsync(string output, MainViewModel<string> viewModel)
        {
            if (output.Length >= 8)
            {
                if (int.TryParse(
                        output.Substring(0, 8),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out var hwnd))
                {
                    var hwndPtr = new IntPtr(hwnd);
                    var callback = onSelect;
                    callback(hwndPtr, state);
                }
            }

            return Task.CompletedTask;
        }
    }

    private unsafe class NativeItemsListMenuDefinitionProvider(
        delegate* unmanaged<void*, byte**> nativeItemsAction,
        delegate* unmanaged<byte*, void*, void> onSelect,
        delegate* unmanaged<void> onClosed,
        void* state) : IMenuDefinitionProvider<string>
    {
        public MenuDefinition<string> Get()
        {
            IEnumerable<string> ConvertToManagedStrings()
            {
                if (nativeItemsAction == null) return [];

                byte** nativeArray = nativeItemsAction(state);
                if (nativeArray == null) return [];

                var result = new List<string>();
                for (byte** ptr = nativeArray; *ptr != null; ptr++)
                {
                    string managedString = Marshal.PtrToStringAnsi((IntPtr)(*ptr)) ?? string.Empty;
                    result.Add(managedString);
                }

                return result;
            }

            return new MenuDefinition<string>
            {
                AsyncFunction = null,
                ItemsFunction = ConvertToManagedStrings,
                HasPreview = false,
                Header = null,
                MinScore = 0,
                QuitOnEscape = false,
                ResultHandler = new NativeResultHandler(onSelect, state),
                OnClosed = () => onClosed()
            };
        }
    }
    
    private unsafe class NativeResultHandler(delegate* unmanaged<byte*, void*, void> onSelect, void* state) : IResultHandler<string>, IResultHandler<FileSystemNode>
    {
        private Task HandleAsync(string output)
        {
            byte[] message = Encoding.UTF8.GetBytes(output + '\0');
            fixed (byte* messagePtr = message)
            {
                var callback = onSelect;
                callback(messagePtr, state);
            }

            return Task.CompletedTask;
        }

        public Task HandleAsync(string output, MainViewModel<string> viewModel)
        {
            return HandleAsync(output);
        }

        public Task HandleAsync(FileSystemNode output, MainViewModel<FileSystemNode> viewModel)
        {
            var path = output.ToString();
            return HandleAsync(path);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "Initialize")]
    public static void Initialize()
    {
        if (_app == null)
        {
            _appThread = new Thread(() =>
            {
                BuildFileSystemApp()
                    .Start((application, args) => RunApp(application), Array.Empty<string>());
                _app?.Initialize();
            });
            _appThread.SetApartmentState(ApartmentState.STA);
            _appThread.Start();
        }

        while (_app == null || !_app.IsInitialized)
        {
            Thread.Sleep(100);
        }
    }
    
    [UnmanagedCallersOnly(EntryPoint = nameof(ShowFileSystem), CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe void ShowFileSystem(delegate* unmanaged<byte*, void*, void> onSelect, delegate* unmanaged<void> onClosed, void* state)
    {
        var title = "File System";
        var command = new FileSystemMenuDefinitionProvider(
            new FileSystemResultHandler(
                new NativeResultHandler(onSelect, state),
                new ShowDirectoryResultHandler(new NativeResultHandler(onSelect, state), false, true, false, false, () => onClosed()),
                false,
                true),
            5,
            null,
            false,
            true,
            false,
            false,
            _fileSystemViewModel,
            null,
            () => onClosed());
        _app?.RunDefinition(command);
    }
    
    [UnmanagedCallersOnly(EntryPoint = nameof(ShowProgramsList), CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe void ShowProgramsList(delegate* unmanaged<byte*, void*, void> onSelect, delegate* unmanaged<void> onClosed, void* state)
    {
        var title = "Program Launcher";
        var appDirectories = new []{ @"c:\users\eric\AppData\Roaming\Microsoft\Windows\Start Menu",
            @"C:\ProgramData\Microsoft\Windows\Start Menu",
            @"c:\users\eric\AppData\Local\Microsoft\WindowsApps",
            @"c:\users\eric\utilities",
            @"C:\Program Files\sysinternals\"};
        
        var command = new FileSystemMenuDefinitionProvider(
            new NativeResultHandler(onSelect, state),
            5,
            appDirectories,
            false,
            false,
            false,
            true,
            _fileSystemViewModel,
            ProgramComparer,
            () => onClosed());
        _app?.RunDefinition(command);
    }
    
    [UnmanagedCallersOnly(EntryPoint = nameof(ShowWindowsList), CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe void ShowWindowsList(delegate* unmanaged<IntPtr, void*, void> onSelect, delegate* unmanaged<void> onClosed, void* state)
    {
        var command = new ShowWindowsMenuDefinitionProvider(
            new ListWindowsNativeResultHandler(onSelect, state), () => onClosed());
        _app?.RunDefinition(command);
    }
    
    [UnmanagedCallersOnly(EntryPoint = nameof(ShowProcessesList), CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe void ShowProcessesList(delegate* unmanaged<byte*, void*, void> onSelect, delegate* unmanaged<void> onClosed, void* state)
    {
        var command = new ShowProcessesMenuDefinitionProvider2(_stringViewModel, ()=> onClosed());
        _app?.RunDefinition(command);
    }
    
    [UnmanagedCallersOnly(EntryPoint = nameof(ShowItemsList), CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe void ShowItemsList(
        delegate* unmanaged<void*, byte**> nativeItemsAction,
        delegate* unmanaged<byte*, void*, void> onSelect,
        delegate* unmanaged<void> onClosed,
        void* state)
    {
        var command = new NativeItemsListMenuDefinitionProvider(nativeItemsAction, onSelect, onClosed, state);
        _app?.RunDefinition(command);
    }

    [UnmanagedCallersOnly(EntryPoint = "Hide")]
    public static void Hide()
    {
        _ = HideAsync();
    }

    private static async Task HideAsync()
    {
        try
        {
            await _currentViewModel!.Close();
        }
        catch (Exception)
        {
            //TODO: Logging
        }
    }
    
    private static AppBuilder BuildFileSystemApp() 
        => AppBuilder.Configure(() =>
        {
            _fileSystemViewModel = new MainViewModel<FileSystemNode>();
            _fileSystemViewModel.GlobalKeyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
            _app = new App(_stringViewModel, _fileSystemViewModel);
            return _app;
        }).UsePlatformDetect();

    private static void RunApp(Application app)
    {
        app.Run(CancellationToken.None);
    } 
    
    private static readonly IComparer<Entry<FileSystemNode>> ProgramComparer = Comparer<Entry<FileSystemNode>>.Create((x, y) =>
    {
        static int GetExtensionPriority(string line)
        {
            // Define extensions to prioritize
            string[] prioritizedExtensions = { ".exe", ".lnk", ".com", ".bat", ".cmd" };

            // Check if the line ends with one of the prioritized extensions
            foreach (var ext in prioritizedExtensions)
            {
                if (line.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    return 1; // Higher priority
            }
            return 0; // Default priority
        }

        // Compare based on score
        int scoreComparison = y.Score.CompareTo(x.Score);
        if (scoreComparison != 0) return scoreComparison;

        // Compare based on extension priority
        var strB = y.Line.ToString();
        var strA = x.Line.ToString();
        int extensionPriorityComparison = GetExtensionPriority(strB).CompareTo(GetExtensionPriority(strA));
        if (extensionPriorityComparison != 0) return extensionPriorityComparison;

        // Compare based on line length
        int lengthComparison = strA.Length.CompareTo(strB.Length);
        if (lengthComparison != 0) return lengthComparison;

        // Compare based on string content
        return string.Compare(strA, strB, StringComparison.Ordinal);
    });
}