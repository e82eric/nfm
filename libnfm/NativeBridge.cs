using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Logging.EventSource;
using nfm.FileSystem;
using nfm.ListProcesses;
using nfm.ListWindows;
using nfm.Ui.Core;
using nfm.Win32Ui;
using nfzf;

namespace nfm.NativeBridge;

[SupportedOSPlatform("windows")]
public static class NativeBridge
{
    private static readonly ILoggerFactory LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => {
        b.AddEventSourceLogger();

        if (OperatingSystem.IsWindows())
        {
            b.AddEventLog(new EventLogSettings
            {
                SourceName = "SimpleWindowManager",
                LogName    = "Application",
                MachineName = ".",
            });

            b.AddFilter<EventLogLoggerProvider>(null, LogLevel.Warning);
            b.AddFilter<EventSourceLoggerProvider>(null, LogLevel.Information);
        }
    });
    
    private static Thread? _appThread;
    private static readonly ViewModel ViewModel = new(LoggerFactory);
    
    private unsafe class ListWindowsNativeResultHandler(delegate* unmanaged<IntPtr, void*, void> onSelect, void* state) : IResultHandler
    {
        public Task HandleAsync(object objOutput)
        {
            if (objOutput is ListWindows.ListWindows.ListWindowsItem lwi)
            {
                var callback = onSelect;
                callback(lwi.Hwnd, state);
            }

            return Task.CompletedTask;
        }
    }

    private unsafe class NativeItemsListMenuDefinitionProvider : IMenuDefinitionProvider
    {
        private readonly MenuDefinition _menuDefinition;
        private readonly delegate* unmanaged<void*, byte**> _nativeItemsAction;
        private readonly void* _state;

        public NativeItemsListMenuDefinitionProvider(
            byte* header,
            delegate* unmanaged<void*, byte**> nativeItemsAction,
            delegate* unmanaged<byte*, void*, void> onSelect,
            delegate* unmanaged<void> onClosed,
            void* state)
        {
            _nativeItemsAction = nativeItemsAction;
            _state = state;

            _menuDefinition = new MenuDefinition
            {
                AsyncFunction = ConvertToManagedStrings,
                ResultHandler = new NativeResultHandler(onSelect, _state),
                OnClosed = () => onClosed(),
                Header = header != null ? Marshal.PtrToStringAnsi((IntPtr)header) : null,
                ScoreFunc = (item, pattern, slab) =>
                {
                    var text = (string)item;
                    var score = FuzzySearcher.GetScore(text, pattern, slab);
                    return (text.Length, score);
                }
            };
            return;

            Task ConvertToManagedStrings(ChannelWriter<object> writer, CancellationToken _)
            {
                if (_nativeItemsAction == null)
                {
                    writer.Complete();
                    return Task.CompletedTask;
                };

                byte** nativeArray = _nativeItemsAction(_state);
                if (nativeArray == null)
                {
                    writer.Complete();
                    return Task.CompletedTask;
                }

                for (byte** ptr = nativeArray; *ptr != null; ptr++)
                {
                    string managedString = Marshal.PtrToStringAnsi((IntPtr)(*ptr)) ?? string.Empty;
                    writer.TryWrite(managedString);
                }
                
                writer.Complete();
                return Task.CompletedTask;
            }
        }

        public MenuDefinition Get()
        {
            return _menuDefinition;
        }
    }
    
    private unsafe class NativeResultHandler(delegate* unmanaged<byte*, void*, void> onSelect, void* state) : IResultHandler
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

        public Task HandleAsync(object output)
        {
            if (output == null)
            {
                return Task.CompletedTask;
            }

            var outputStr = output.ToString();
            if (outputStr == null)
            {
                return Task.CompletedTask;
            }
            
            return HandleAsync(outputStr);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "Initialize")]
    public static void Initialize()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("This functionality is only supported on Windows.");
        }
        _appThread = new Thread(() =>
        {
            ViewModel.GlobalKeyBindings.Add((ModifierKeys.LCtl, VirtualKeyCodes.VK_C), ClipboardHelper.CopyStringToClipboard);
            ViewModel.GlobalKeyBindings.Add((ModifierKeys.LCtl, VirtualKeyCodes.VK_P), (o, model) =>
            {
                ViewModel.TogglePreview();
                return Task.CompletedTask;
            });
            var window = new Win32Window(LoggerFactory, ViewModel, () => { });
            window.Run();
        });
        _appThread.SetApartmentState(ApartmentState.STA);
        _appThread.Start();
    }
    
    [UnmanagedCallersOnly(EntryPoint = nameof(ShowFileSystem), CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe void ShowFileSystem(delegate* unmanaged<byte*, void*, void> onSelect, delegate* unmanaged<void> onClosed, void* state)
    {
        var showPreview = false;
        var command = new FileSystemMenuDefinitionProvider(
            new FileSystemResultHandler(
                ViewModel,
                new NativeResultHandler(onSelect, state),
                new ShowDirectoryResultHandler(
                    ViewModel,
                    new NativeResultHandler(onSelect, state),
                    false,
                    showPreview,
                    false,
                    false,
                    () => onClosed()),
                false,
                true),
            5,
            null,
            false,
            showPreview,
            false,
            false,
            true,
            null,
            null,
            null,
            false,
            ViewModel,
            Comparers.ScoreLengthAndValue,
            () => onClosed(),
            null);
        
        RunDefinition(command.Get());
    }
    
    [UnmanagedCallersOnly(EntryPoint = nameof(ShowProgramsList), CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe void ShowProgramsList(byte** directories, int directoryCount, delegate* unmanaged<byte*, void*, void> onSelect, delegate* unmanaged<void> onClosed, void* state)
    {
        var appDirectories = new string[directoryCount];
        for (int i = 0; i < directoryCount; i++)
        {
            appDirectories[i] = Marshal.PtrToStringAnsi((IntPtr)directories[i]) ?? string.Empty;
        }
        
        var command = new FileSystemMenuDefinitionProvider(
            new NativeResultHandler(onSelect, state),
            5,
            appDirectories,
            false,
            false,
            false,
            true,
            true,
            null,
            null,
            null,
            false,
            ViewModel,
            ProgramComparer,
            () => onClosed(),
            null);
        
        RunDefinition(command.Get());
    }
    
    [UnmanagedCallersOnly(EntryPoint = nameof(ShowWindowsList), CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe void ShowWindowsList(delegate* unmanaged<IntPtr, void*, void> onSelect, delegate* unmanaged<void> onClosed, void* state)
    {
        var command = new ShowWindowsMenuDefinitionProvider2(
            new ListWindowsNativeResultHandler(onSelect, state), () => onClosed());
        
        RunDefinition(command.Get());
    }
    
    [UnmanagedCallersOnly(EntryPoint = nameof(ShowProcessesList), CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe void ShowProcessesList(
        delegate* unmanaged<byte*, void*, void> onSelect,
        delegate* unmanaged<void> onClosed,
        void* state)
    {
        var command = new ShowProcessesMenuDefinitionProvider(
            LoggerFactory,
            ViewModel,
            () => onClosed(),
            new NativeResultHandler(onSelect, state));
        RunDefinition(command.Get());
    }
    
    [UnmanagedCallersOnly(EntryPoint = nameof(ShowItemsList), CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe void ShowItemsList(
        byte* header,
        delegate* unmanaged<void*, byte**> nativeItemsAction,
        delegate* unmanaged<byte*, void*, void> onSelect,
        delegate* unmanaged<void> onClosed,
        void* state)
    {
        var command = new NativeItemsListMenuDefinitionProvider(header, nativeItemsAction, onSelect, onClosed, state);
        RunDefinition(command.Get());
    }
    
    [UnmanagedCallersOnly(EntryPoint = nameof(SetMenuLocation), CallConvs = [typeof(CallConvCdecl)])]
    public static void SetMenuLocation(int x, int y)
    {
        ViewModel.RequestedX = x;
        ViewModel.RequestedY = y;
    }

    [UnmanagedCallersOnly(EntryPoint = nameof(RunLastDefinition), CallConvs = [typeof(CallConvCdecl)])]
    public static void RunLastDefinition()
    {
        ViewModel.ShowLastDefinition();
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
            await ViewModel.Close(false);
        }
        catch (Exception)
        {
            //TODO: Logging
        }
    }

    private static void RunDefinition(MenuDefinition definition)
    {
        Task.Run(async () =>
        {
            await ViewModel.RunDefinitionAsync(definition);
        });
    }
    
    private static readonly IComparer<Entry> ProgramComparer = Comparer<Entry>.Create((x, y) =>
    {
        static int GetExtensionPriority(string line)
        {
            string[] prioritizedExtensions = { ".exe", ".lnk", ".com", ".bat", ".cmd" };

            foreach (var ext in prioritizedExtensions)
            {
                if (line.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    return 1;
            }
            return 0;
        }

        int scoreComparison = y.Score.CompareTo(x.Score);
        if (scoreComparison != 0) return scoreComparison;

        var strB = y.Item.ToString();
        var strA = x.Item.ToString();
        if (strA == null || strB == null)
        {
            return scoreComparison;
        }
        
        int extensionPriorityComparison = GetExtensionPriority(strB).CompareTo(GetExtensionPriority(strA));
        if (extensionPriorityComparison != 0) return extensionPriorityComparison;

        int lengthComparison = strA.Length.CompareTo(strB.Length);
        if (lengthComparison != 0) return lengthComparison;

        return string.Compare(strA, strB, StringComparison.Ordinal);
    });

    private class ArrayColumnRow
    {
        public object[] Data { get; }
        public int[] ColumnWidths { get; }
        public bool IsHeader { get; }
        public int[]? DisplayColumnIndices { get; }

        public ArrayColumnRow(object[] data, int[] columnWidths, int[]? displayColumnIndices = null, bool isHeader = false)
        {
            Data = data;
            ColumnWidths = columnWidths;
            DisplayColumnIndices = displayColumnIndices;
            IsHeader = isHeader;
        }

        public override string ToString()
        {
            if (DisplayColumnIndices == null || DisplayColumnIndices.Length == 0)
            {
                return string.Join("  ", 
                    Data.Select((cell, i) => 
                    {
                        var text = cell?.ToString() ?? string.Empty;
                        return i < ColumnWidths.Length ? text.PadRight(ColumnWidths[i]) : text;
                    }));
            }

            var displayItems = new List<string>();
            for (int i = 0; i < DisplayColumnIndices.Length; i++)
            {
                int colIndex = DisplayColumnIndices[i];
                if (colIndex >= 0 && colIndex < Data.Length)
                {
                    var text = Data[colIndex]?.ToString() ?? string.Empty;
                    var paddedText = i < ColumnWidths.Length ? text.PadRight(ColumnWidths[i]) : text;
                    displayItems.Add(paddedText);
                }
                else
                {
                    var paddedText = i < ColumnWidths.Length ? string.Empty.PadRight(ColumnWidths[i]) : string.Empty;
                    displayItems.Add(paddedText);
                }
            }
            
            return string.Join("  ", displayItems);
        }
    }

    private unsafe class NativeArrayColumnMenuDefinitionProvider : IMenuDefinitionProvider
    {
        private readonly MenuDefinition _menuDefinition;

        public NativeArrayColumnMenuDefinitionProvider(
            byte*** arrayData,
            int rowCount,
            int columnCount,
            byte** columnNames,
            int columnNamesCount,
            int* displayColumnIndices,
            int displayColumnCount,
            int showPreview,
            delegate* unmanaged<byte*, void*, void> onSelect,
            delegate* unmanaged<void> onClosed,
            void* state)
        {
            var managedData = ConvertNativeArrayToManaged(arrayData, rowCount, columnCount);
            
            string[]? managedColumnNames = null;
            if (columnNames != null && columnNamesCount > 0)
            {
                managedColumnNames = new string[columnNamesCount];
                for (int i = 0; i < columnNamesCount; i++)
                {
                    managedColumnNames[i] = Marshal.PtrToStringAnsi((IntPtr)columnNames[i]) ?? string.Empty;
                }
            }

            int[]? managedDisplayColumnIndices = null;
            if (displayColumnIndices != null && displayColumnCount > 0)
            {
                managedDisplayColumnIndices = new int[displayColumnCount];
                for (int i = 0; i < displayColumnCount; i++)
                {
                    managedDisplayColumnIndices[i] = displayColumnIndices[i];
                }
            }

            var displayColumnNames = FilterColumnNamesForDisplay(managedColumnNames, managedDisplayColumnIndices);
            var columnWidths = CalculateColumnWidths(managedData, displayColumnNames, managedDisplayColumnIndices);

            var rows = new List<ArrayColumnRow>();
            foreach (var row in managedData)
            {
                rows.Add(new ArrayColumnRow(row, columnWidths, managedDisplayColumnIndices));
            }

            var header = displayColumnNames != null && displayColumnNames.Length > 0
                ? string.Join("  ", displayColumnNames.Select((name, i) => i < columnWidths.Length ? name.PadRight(columnWidths[i]) : name))
                : null;

            _menuDefinition = new MenuDefinition
            {
                AsyncFunction = (writer, ct) =>
                {
                    foreach (var row in rows)
                    {
                        writer.WriteAsync(row, ct);
                    }
                    writer.Complete();
                    return Task.CompletedTask;
                },
                ResultHandler = new NativeArrayColumnResultHandler(onSelect, state),
                Header = header,
                Comparer = Comparers.ScoreLengthAndValue,
                FinalComparer = Comparers.ScoreLengthAndValue,
                OnClosed = () => onClosed(),
                HasPreview = showPreview != 0,
                PreviewHandler = showPreview != 0 ? new NativeArrayColumnPreviewHandler(managedColumnNames) : null,
                ScoreFunc = (sObj, pattern, slab) =>
                {
                    var s = sObj.ToString() ?? string.Empty;
                    var result = FuzzySearcher.GetScore(s, pattern, slab);
                    return (s.Length, result);
                }
            };
        }

        public MenuDefinition Get() => _menuDefinition;


        private static string[]? FilterColumnNamesForDisplay(string[]? originalColumnNames, int[]? displayColumnIndices)
        {
            if (originalColumnNames == null || displayColumnIndices == null || displayColumnIndices.Length == 0)
            {
                return originalColumnNames;
            }

            var filteredNames = new string[displayColumnIndices.Length];
            for (int i = 0; i < displayColumnIndices.Length; i++)
            {
                int colIndex = displayColumnIndices[i];
                if (colIndex >= 0 && colIndex < originalColumnNames.Length)
                {
                    filteredNames[i] = originalColumnNames[colIndex];
                }
                else
                {
                    filteredNames[i] = $"Column {colIndex + 1}";
                }
            }
            return filteredNames;
        }

        private static object[][] ConvertNativeArrayToManaged(byte*** arrayData, int rowCount, int columnCount)
        {
            var result = new object[rowCount][];
            
            for (int row = 0; row < rowCount; row++)
            {
                result[row] = new object[columnCount];
                for (int col = 0; col < columnCount; col++)
                {
                    result[row][col] = Marshal.PtrToStringAnsi((IntPtr)arrayData[row][col]) ?? string.Empty;
                }
            }
            
            return result;
        }

        private static int[] CalculateColumnWidths(object[][] data, string[]? columnNames, int[]? displayColumnIndices)
        {
            if (data.Length == 0)
            {
                return [];
            }

            if (displayColumnIndices == null || displayColumnIndices.Length == 0)
            {
                int columnCount = data[0].Length;
                displayColumnIndices = new int[columnCount];
                for (int i = 0; i < columnCount; i++)
                {
                    displayColumnIndices[i] = i;
                }
            }

            var columnWidths = new int[displayColumnIndices.Length];

            if (columnNames != null)
            {
                for (int i = 0; i < Math.Min(displayColumnIndices.Length, columnNames.Length); i++)
                {
                    columnWidths[i] = Math.Max(columnWidths[i], columnNames[i].Length);
                }
            }

            foreach (var row in data)
            {
                for (int i = 0; i < displayColumnIndices.Length; i++)
                {
                    int colIndex = displayColumnIndices[i];
                    if (colIndex >= 0 && colIndex < row.Length)
                    {
                        var text = row[colIndex]?.ToString() ?? string.Empty;
                        columnWidths[i] = Math.Max(columnWidths[i], text.Length);
                    }
                }
            }

            return columnWidths;
        }
    }

    private unsafe class NativeArrayColumnResultHandler : IResultHandler
    {
        private readonly delegate* unmanaged<byte*, void*, void> _onSelect;
        private readonly void* _state;

        public NativeArrayColumnResultHandler(delegate* unmanaged<byte*, void*, void> onSelect, void* state)
        {
            _onSelect = onSelect;
            _state = state;
        }

        public Task HandleAsync(object objOutput)
        {
            var output = objOutput?.ToString() ?? string.Empty;
            var outputBytes = Encoding.UTF8.GetBytes(output);
            
            fixed (byte* outputPtr = outputBytes)
            {
                _onSelect(outputPtr, _state);
            }
            
            return Task.CompletedTask;
        }
    }

    private class NativeArrayColumnPreviewHandler : IPreviewHandler
    {
        private readonly string[]? _allColumnNames;

        public NativeArrayColumnPreviewHandler(string[]? allColumnNames)
        {
            _allColumnNames = allColumnNames;
        }

        public Task Handle(IPreviewRenderer renderer, object item, int height, CancellationToken cancellationToken)
        {
            try
            {
                if (item is not ArrayColumnRow selectedRow)
                {
                    renderer.RenderError($"Invalid item type for preview {item?.GetType()}");
                    return Task.CompletedTask;
                }

                var previewLines = new List<string>();

                for (int i = 0; i < selectedRow.Data.Length; i++)
                {
                    var columnName = (_allColumnNames != null && i < _allColumnNames.Length) 
                        ? _allColumnNames[i] 
                        : $"Column {i + 1}";
                    var cellValue = selectedRow.Data[i]?.ToString() ?? string.Empty;
                    previewLines.Add($"{columnName}: {cellValue}");
                }

                List<List<TextSegment>> rows = new List<List<TextSegment>>();
                foreach (var line in previewLines)
                {
                    var splitLines = line.Split('\n');
                    foreach (var splitLine in splitLines)
                    {
                        var textSegment = new TextSegment { State = new AnsiState(), Text = splitLine };
                        rows.Add([textSegment]);
                    }
                }

                renderer.RenderText(rows, 0);
            }
            catch (Exception ex)
            {
                renderer.RenderError($"Preview error: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "ShowArrayColumns", CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe void ShowArrayColumnsMenu(
        byte*** arrayData,
        int rowCount,
        int columnCount,
        byte** columnNames,
        int columnNamesCount,
        int* displayColumnIndices,
        int displayColumnCount,
        int showPreview,
        delegate* unmanaged<byte*, void*, void> onSelect,
        delegate* unmanaged<void> onClosed,
        void* state)
    {
        var provider = new NativeArrayColumnMenuDefinitionProvider(
            arrayData, rowCount, columnCount, 
            columnNames, columnNamesCount, 
            displayColumnIndices, displayColumnCount, showPreview,
            onSelect, onClosed, state);
        RunDefinition(provider.Get());
    }
}