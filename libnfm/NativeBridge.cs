using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Channels;
using Core;
using nfzf;
using Win32FromForms;

namespace nfm.menu;

[SupportedOSPlatform("windows")]
public static class NativeBridge
{
    private static Thread? _appThread;
    private static readonly ViewModel ViewModel = new();
    
    private unsafe class ListWindowsNativeResultHandler(delegate* unmanaged<IntPtr, void*, void> onSelect, void* state) : IResultHandler
    {
        public Task HandleAsync(object objOutput)
        {
            var output = (string)objOutput;
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
            var window = new Win32Window(ViewModel, () => { });
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
            () => onClosed());
        
        RunDefinition(command.Get());
    }
    
    [UnmanagedCallersOnly(EntryPoint = nameof(ShowProgramsList), CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe void ShowProgramsList(delegate* unmanaged<byte*, void*, void> onSelect, delegate* unmanaged<void> onClosed, void* state)
    {
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
            true,
            null,
            null,
            null,
            false,
            ViewModel,
            ProgramComparer,
            () => onClosed());
        
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
        var command = new ShowProcessesMenuDefinitionProvider(ViewModel, () => onClosed());
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

        public ArrayColumnRow(object[] data, int[] columnWidths, bool isHeader = false)
        {
            Data = data;
            ColumnWidths = columnWidths;
            IsHeader = isHeader;
        }

        public override string ToString()
        {
            return string.Join("  ", 
                Data.Select((cell, i) => 
                {
                    var text = cell?.ToString() ?? string.Empty;
                    return i < ColumnWidths.Length ? text.PadRight(ColumnWidths[i]) : text;
                }));
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

            // Calculate column widths
            var columnWidths = CalculateColumnWidths(managedData, managedColumnNames);

            // Create row objects (only data rows, no header in the items list)
            var rows = new List<ArrayColumnRow>();

            // Add data rows only
            foreach (var row in managedData)
            {
                rows.Add(new ArrayColumnRow(row, columnWidths));
            }

            // Use column names as header if available
            var header = managedColumnNames != null && managedColumnNames.Length > 0
                ? string.Join("  ", managedColumnNames.Select((name, i) => i < columnWidths.Length ? name.PadRight(columnWidths[i]) : name))
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
                PreviewHandler = showPreview != 0 ? new NativeArrayColumnPreviewHandler(managedData, managedColumnNames, rows) : null,
                ScoreFunc = (sObj, pattern, slab) =>
                {
                    var s = sObj.ToString() ?? string.Empty;
                    var result = FuzzySearcher.GetScore(s, pattern, slab);
                    return (s.Length, result);
                }
            };
        }

        public MenuDefinition Get() => _menuDefinition;

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

        private static int[] CalculateColumnWidths(object[][] data, string[]? columnNames)
        {
            if (data.Length == 0) return [];

            int columnCount = data[0].Length;
            var columnWidths = new int[columnCount];

            // Measure column widths including headers
            if (columnNames != null)
            {
                for (int col = 0; col < Math.Min(columnCount, columnNames.Length); col++)
                {
                    columnWidths[col] = Math.Max(columnWidths[col], columnNames[col].Length);
                }
            }

            // Measure data column widths
            foreach (var row in data)
            {
                for (int col = 0; col < Math.Min(columnCount, row.Length); col++)
                {
                    var text = row[col]?.ToString() ?? string.Empty;
                    columnWidths[col] = Math.Max(columnWidths[col], text.Length);
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
        private readonly object[][] _data;
        private readonly string[]? _columnNames;
        private readonly List<ArrayColumnRow> _rows;

        public NativeArrayColumnPreviewHandler(object[][] data, string[]? columnNames, List<ArrayColumnRow> rows)
        {
            _data = data;
            _columnNames = columnNames;
            _rows = rows;
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
                    var columnName = (_columnNames != null && i < _columnNames.Length) 
                        ? _columnNames[i] 
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
    public static unsafe void ShowArrayColumns(
        byte*** arrayData,
        int rowCount,
        int columnCount,
        byte** columnNames,
        int columnNamesCount,
        int showPreview,
        delegate* unmanaged<byte*, void*, void> onSelect,
        delegate* unmanaged<void> onClosed,
        void* state)
    {
        var provider = new NativeArrayColumnMenuDefinitionProvider(
            arrayData, rowCount, columnCount, 
            columnNames, columnNamesCount, showPreview,
            onSelect, onClosed, state);
        RunDefinition(provider.Get());
    }
}