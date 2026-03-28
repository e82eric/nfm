using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;
using nfm.FileSystem;
using nfm.Ui.Core;
using nfm.Win32Ui;
using nfm.ListProcesses;
using nfm.ListWindows;

namespace nfm.Cli;

internal sealed record StdInOptions(
    string? Header = null,
    string? EditCommand = null,
    bool ShowPreview = false,
    string? PreviewCommand = null,
    char? Delimiter = null,
    string? PreviewStartLineCommand = null,
    string? PreviewStartLineOffsetCommand = null,
    bool ShowGap = false,
    string? LineContinuation = null,
    bool WrapLines = false,
    string? SearchString = null,
    bool NoLengthSort = false,
    bool ExcludeTopmost = false
);

internal sealed record FileSystemOptions(
    bool SearchDirectoryOnSelect = false,
    string RootDirectory = "",
    int MaxDepth = int.MaxValue,
    bool DirectoriesOnly = false,
    bool FilesOnly = false,
    char? Delimiter = null,
    string? PreviewStartLineCommand = null,
    string? PreviewStartLineOffsetCommand = null,
    bool WrapLines = false,
    bool ShowGap = false,
    bool ShowPreview = false,
    bool ExcludeTopmost = false,
    string? SearchString = null
);

internal sealed record CommandOptions(
    IEnumerable<string>? Command
);

internal sealed record CsvOptions(
    int[]? ColumnIndices = null,
    string[]? DisplayColumns = null,
    string[]? Headers = null,
    bool HasHeader = true,
    char Delimiter = ',',
    bool DisablePreview = false,
    bool ExcludeTopmost = false
);

internal sealed record ProcessOptions(
    bool ExcludeTopmost = false,
    bool SortByCpu = false,
    bool SortByPrivateBytes = false,
    bool SortByWorkingSet = false,
    bool SortByPid = false
);

[SupportedOSPlatform("windows")]
class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var loggerFactory = LoggerFactory.Create(b =>
        {
            var level = LogLevel.Warning;
            foreach (var a in args)
            {
                switch (a)
                {
                    case "--debug":
                    case "-d":
                        level = LogLevel.Debug;
                        break;
                }
            }
            
            b.AddConsole(o =>
            {
                o.LogToStandardErrorThreshold = level;
            });
        
            b.SetMinimumLevel(level);
        });

        args = args.Where(a => a != "--debug" && a != "-d").ToArray();
        var log = loggerFactory.CreateLogger<Program>();
        log.LogDebug("Starting");
        
        var viewModel = new ViewModel(loggerFactory);
        viewModel.GlobalKeyBindings.Add((ModifierKeys.LCtl, VirtualKeyCodes.VK_C), ClipboardHelper.CopyStringToClipboard);
        viewModel.GlobalKeyBindings.Add((ModifierKeys.LCtl, VirtualKeyCodes.VK_P), (_, _) =>
        {
            viewModel.TogglePreview();
            return Task.CompletedTask;
        });
        
        if (Console.IsInputRedirected)
        {
            log.LogDebug("Reading from stdin");
            try
            {
                int nextChar = Console.In.Peek();
                if (nextChar != -1)
                {
                    if (args.Length > 0 && args[0] == "csv")
                    {
                        log.LogDebug("Reading input as csv");
                        var csvOptions = ParseCsvOptions(args, log);
                        if (csvOptions != null)
                        {
                            var csvProvider = CreateCsvStringArrayColumnProvider(loggerFactory, viewModel, csvOptions);
                            var window = new Win32Window(loggerFactory, viewModel, () =>
                            {
                                Task.Run(async () =>
                                {
                                    try
                                    {
                                        await viewModel.RunDefinitionAsync(csvProvider.Get());
                                    }
                                    catch (Exception e)
                                    {
                                        log.LogError(e, "Error occured running csv definition");
                                    }
                                });
                            }, csvOptions.ExcludeTopmost);
                            window.Run();
                            return;
                        }
                    }
                    else
                    {
                        log.LogDebug("Running using stdin");
                        var stdInOptions = ParseStdInOptions(args, log);
                        if (stdInOptions != null)
                        {
                            var menuDefinitionProvider = new StdInMenuDefinitionProvider(
                                loggerFactory,
                                viewModel,
                                stdInOptions.ShowPreview,
                                null,
                                stdInOptions.PreviewCommand,
                                stdInOptions.Delimiter,
                                stdInOptions.PreviewStartLineCommand,
                                stdInOptions.PreviewStartLineOffsetCommand,
                                stdInOptions.Header,
                                stdInOptions.LineContinuation,
                                stdInOptions.SearchString,
                                stdInOptions.WrapLines,
                                stdInOptions.NoLengthSort ? Comparers.ScoreOnly : Comparers.ScoreLengthAndValue,
                                stdInOptions.ShowGap);
                            var window = new Win32Window(loggerFactory, viewModel,() =>
                            {
                                Task.Run(async () =>
                                {
                                    try
                                    {
                                        await viewModel.RunDefinitionAsync(menuDefinitionProvider.Get());
                                    }
                                    catch (Exception e)
                                    {
                                        log.LogError(e, "Error running stdin definition");
                                    }
                                });
                            }, stdInOptions.ExcludeTopmost);
                            window.Run();
                            return;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.LogError(e, "Error occured");
            }
        }
        
        if (args.Length > 0)
        {
            if (args[0] == "filesystem")
            {
                log.LogDebug("Running as filesystem");
                var fileSystemOptions = ParseFileSystemOptions(args, log);
                if (fileSystemOptions is null)
                {
                    return;
                }
                var definitionProvider = new FileSystemMenuDefinitionProvider(
                    new StdOutResultHandler(viewModel),
                    fileSystemOptions.MaxDepth,
                    [fileSystemOptions.RootDirectory],
                    true,
                    fileSystemOptions.ShowPreview,
                    fileSystemOptions.DirectoriesOnly,
                    fileSystemOptions.FilesOnly,
                    fileSystemOptions.WrapLines,
                    fileSystemOptions.Delimiter,
                    fileSystemOptions.PreviewStartLineCommand,
                    fileSystemOptions.PreviewStartLineOffsetCommand,
                    fileSystemOptions.ShowGap,
                    viewModel,
                    null,
                    null,
                    fileSystemOptions.SearchString);
                var window = new Win32Window(loggerFactory, viewModel,() =>
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await viewModel.RunDefinitionAsync(definitionProvider.Get());
                        }
                        catch (Exception e)
                        {
                            log.LogError(e, "Error occured running file system definition");
                            throw;
                        }
                    });
                }, fileSystemOptions.ExcludeTopmost);
                window.Run();
            }
            else if (args[0] == "command")
            {
                var commandOptions = ParseCommandOptions(args);
                if (commandOptions != null)
                {
                    //BuildCommandApp(string.Join(" ", commandOptions.Command))
                    //    .Start((application, strings) => Run(application, false), args);
                    //return;
                }
            }
            else if (args[0] == "processes")
            {
                var processOptions = ParseProcessOptions(args, log);
                if (processOptions != null)
                {
                    var processProvider = CreateProcessStringArrayColumnProvider(loggerFactory, viewModel, processOptions);
                    var window = new Win32Window(loggerFactory, viewModel, () =>
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                await viewModel.RunDefinitionAsync(processProvider.Get());
                            }
                            catch(Exception e)
                            {
                                log.LogError(e, "Error occured running definition");
                            }
                        });
                    }, processOptions.ExcludeTopmost);
                    window.Run();
                }
            }
            else if (args[0] == "windows")
            {
                var windowsProvider = new ShowWindowsMenuDefinitionProvider2(
                    new StdOutResultHandler(viewModel), null);
                var window = new Win32Window(loggerFactory, viewModel, () =>
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await viewModel.RunDefinitionAsync(windowsProvider.Get());
                        }
                        catch (Exception e)
                        {
                            log.LogError(e, "Error occured running windows definition");
                        }
                    });
                });
                window.Run();
            }
        }
    }
    
    private static StdInOptions? ParseStdInOptions(string[] args, ILogger log)
    {
        var options = new StdInOptions();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--header" && i + 1 < args.Length)
            {
                options = options with { Header = args[i + 1] };
                i++;
            }
            else if (args[i] == "--editcommand" && i + 1 < args.Length)
            {
                options = options with { EditCommand = args[i + 1] };
                i++;
            }
            else if (args[i] == "--previewcommand" && i + 1 < args.Length)
            {
                options = options with {PreviewCommand = args[i + 1] };
                i++;
            }
            else if (args[i] == "--searchstring" && i + 1 < args.Length)
            {
                options = options with { SearchString = args[i + 1] };
                i++;
            }
            else if (args[i] == "--previewstartlinecommand" && i + 1 < args.Length)
            {
                options = options with { PreviewStartLineCommand = args[i + 1] };
                i++;
            }
            else if (args[i] == "--previewstartlineoffsetcommand" && i + 1 < args.Length)
            {
                options = options with { PreviewStartLineOffsetCommand = args[i + 1] };
                i++;
            }
            else if (args[i] == "--delimiter" && i + 1 < args.Length)
            {
                if (args[i + 1].Length == 1)
                {
                    options = options with { Delimiter = args[i + 1][0] };
                }
                i++;
            }
            else if (args[i] == "--gap")
            {
                options = options with { ShowGap = true };
            }
            else if (args[i] == "--showpreview")
            {
                options = options with { ShowPreview = true };
            }
            else if (args[i] == "--nolengthsort")
            {
                options = options with { NoLengthSort = true };
            }
            else if (args[i] == "--wrap")
            {
                options = options with { WrapLines = true };
            }
            else if (args[i] == "--linecontinuation")
            {
                if (args[i + 1].Length == 1)
                {
                    options = options with { LineContinuation = args[i + 1] };
                }

                i++;
            }
            else if (args[i] == "--exclude-topmost" || args[i] == "--no-topmost")
            {
                options = options with { ExcludeTopmost = true };
            }
            else
            {
                Console.Error.WriteLine($"Unknown argument: {args[i]}");
                return null;
            }
        }
        
        log.LogDebug("Parsed stdin options: {options}", options);
        return options;
    }
    
    private static FileSystemOptions? ParseFileSystemOptions(string[] args, ILogger log)
    {
        var options = new FileSystemOptions();
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--searchdirectoryonselect")
            {
                options = options with { SearchDirectoryOnSelect = true };
            }
            else if (args[i] == "--rootdirectory" && i + 1 < args.Length)
            {
                options = options with { RootDirectory = args[i + 1] };
                i++;
            }
            else if (args[i] == "--maxdepth" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int maxDepth) && maxDepth > 0)
                {
                    options = options with { MaxDepth = maxDepth };
                    i++;
                }
                else
                {
                    Console.Error.WriteLine("Error: --maxdepth must be a positive integer.");
                    return null;
                }
            }
            else if (args[i] == "--showpreview")
            {
                options = options with { ShowPreview = true };
            }
            else if (args[i] == "--directoriesonly")
            {
                options = options with { DirectoriesOnly = true };
            }
            else if (args[i] == "--filesonly")
            {
                options = options with { FilesOnly = true };
            }
            else if (args[i] == "--wrap")
            {
                options = options with { WrapLines = true };
            }
            else if (args[i] == "--gap")
            {
                options = options with { ShowGap = true };
            }
            else if (args[i] == "--previewstartlinecommand" && i + 1 < args.Length)
            {
                options = options with { PreviewStartLineCommand = args[i + 1] };
                i++;
            }
            else if (args[i] == "--previewstartlineoffsetcommand" && i + 1 < args.Length)
            {
                options = options with { PreviewStartLineOffsetCommand = args[i + 1] };
                i++;
            }
            else if (args[i] == "--delimiter" && i + 1 < args.Length)
            {
                if (args[i + 1].Length == 1)
                {
                    options = options with { Delimiter = args[i + 1][0] };
                }
                i++;
            }
            else if (args[i] == "--exclude-topmost" || args[i] == "--no-topmost")
            {
                options = options with { ExcludeTopmost = true };
            }
            else if (args[i] == "--searchstring" && i + 1 < args.Length)
            {
                options = options with {SearchString = args[i + 1] };
                i++;
            }
            else
            {
                Console.Error.WriteLine($"Unknown argument: {args[i]}");
                return null;
            }
        }

        if (options.DirectoriesOnly && options.FilesOnly)
        {
            log.LogError("Error: --directoriesonly and --filesonly are mutually exclusive.");
            return null;
        }

        if (string.IsNullOrEmpty(options.RootDirectory))
        {
            options = options with { RootDirectory = Directory.GetCurrentDirectory() };
        }

        if (!Directory.Exists(options.RootDirectory))
        {
            Console.Error.WriteLine($"Error: Directory not found: {options.RootDirectory}");
            return null;
        }

        log.LogDebug("Parsed filesystem options: {options}", options);
        return options;
    }
    
    private static CommandOptions? ParseCommandOptions(string[] args)
    {
        var command = args.Skip(1).ToList();
        if (!command.Any())
        {
            Console.Error.WriteLine("Error: No command specified.");
            return null;
        }
        var options = new CommandOptions(command);
        return options;
    }

    private static CsvOptions? ParseCsvOptions(string[] args, ILogger log)
    {
        var options = new CsvOptions();
        for (int i = 1; i < args.Length; i++) // Start from 1 to skip "csv"
        {
            if (args[i] == "--columns" && i + 1 < args.Length)
            {
                var columnStrings = args[i + 1].Split(',');
                var columnIndices = new List<int>();
                var displayColumns = new List<string>();

                foreach (var colStr in columnStrings.Select(s => s.Trim()))
                {
                    if (int.TryParse(colStr, out var colIndex))
                    {
                        columnIndices.Add(colIndex);
                    }
                    else
                    {
                        displayColumns.Add(colStr);
                    }
                }

                if (columnIndices.Count > 0)
                {
                    options = options with { ColumnIndices = columnIndices.ToArray() };
                }
                if (displayColumns.Count > 0)
                {
                    options = options with { DisplayColumns = displayColumns.ToArray() };
                }
                i++;
            }
            else if (args[i] == "--headers" && i + 1 < args.Length)
            {
                options = options with { Headers = args[i + 1].Split(',') };
                i++;
            }
            else if (args[i] == "--delimiter" && i + 1 < args.Length)
            {
                if (args[i + 1].Length == 1)
                {
                    options = options with { Delimiter = args[i + 1][0] };
                }
                i++;
            }
            else if (args[i] == "--no-header")
            {
                options = options with { HasHeader = false };
            }
            else if (args[i] == "--disable-preview")
            {
                options = options with { DisablePreview = true };
            }
            else if (args[i] == "--exclude-topmost" || args[i] == "--no-topmost")
            {
                options = options with { ExcludeTopmost = true };
            }
            else
            {
                Console.Error.WriteLine($"Unknown CSV argument: {args[i]}");
                return null;
            }
        }
        
        log.LogDebug("Parsed csv options {options}", options);
        return options;
    }

    private static ProcessOptions? ParseProcessOptions(string[] args, ILogger log)
    {
        var options = new ProcessOptions();
        for (int i = 1; i < args.Length; i++) // Start from 1 to skip "processes"
        {
            if (args[i] == "--exclude-topmost" || args[i] == "--no-topmost")
            {
                options = options with { ExcludeTopmost = true };
            }
            else if (args[i] == "--sort-cpu")
            {
                options = options with { SortByCpu = true };
            }
            else if (args[i] == "--sort-private-bytes")
            {
                options = options with { SortByPrivateBytes = true };
            }
            else if (args[i] == "--sort-working-set")
            {
                options = options with { SortByWorkingSet = true };
            }
            else if (args[i] == "--sort-pid")
            {
                options = options with { SortByPid = true };
            }
            else
            {
                Console.Error.WriteLine($"Unknown processes argument: {args[i]}");
                return null;
            }
        }
        
        log.LogDebug("Parsed processes options: {options}", options);
        return options;
    }

    private static StringArrayColumnMenuDefinitionProvider CreateCsvStringArrayColumnProvider(ILoggerFactory loggerFactory, IMainViewModel viewModel, CsvOptions options)
    {
        // Read CSV input once
        var csvInput = Console.In.ReadToEnd();
        if (string.IsNullOrWhiteSpace(csvInput))
        {
            Console.Error.WriteLine("No CSV input provided");
            return new StringArrayColumnMenuDefinitionProvider(
                loggerFactory,
                () => new string[0][],
                new int[0],
                null,
                new StdOutResultHandler(viewModel),
                enablePreview: false,
                viewModel: viewModel);
        }

        var lines = csvInput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var data = new List<string[]>();
        string[]? headerRow = null;

        var startIndex = 0;
        if (options.HasHeader && lines.Length > 0)
        {
            headerRow = ParseCsvLine(lines[0], options.Delimiter);
            startIndex = 1;
        }

        for (int i = startIndex; i < lines.Length; i++)
        {
            try
            {
                var row = ParseCsvLine(lines[i], options.Delimiter);
                data.Add(row);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error parsing CSV line {i + 1}: {ex.Message}");
            }
        }

        if (data.Count == 0)
        {
            Console.Error.WriteLine("No data rows found in CSV");
            return new StringArrayColumnMenuDefinitionProvider(
                loggerFactory,
                () => new string[0][],
                new int[0],
                null,
                new StdOutResultHandler(viewModel),
                enablePreview: false,
                viewModel: viewModel);
        }

        // Determine display approach: by column names or indices
        var columnIndices = options.ColumnIndices;
        var displayColumns = options.DisplayColumns;

        // Auto-detect columns if neither indices nor names are specified
        if ((columnIndices == null || columnIndices.Length == 0) &&
            (displayColumns == null || displayColumns.Length == 0))
        {
            // Show first 6 columns or all columns if fewer than 6
            var maxColumns = Math.Min(6, data.Count > 0 ? data[0].Length : 0);
            columnIndices = Enumerable.Range(0, maxColumns).ToArray();
        }

        // If no column indices specified but we have display columns, we'll let the provider resolve them
        if (columnIndices == null || columnIndices.Length == 0)
        {
            columnIndices = new int[0]; // Empty array as fallback
        }

        // Use provided headers or extract from header row for display columns
        var headers = options.Headers;
        if (headers == null && displayColumns != null && displayColumns.Length > 0)
        {
            // Headers will be resolved from displayColumns, so use displayColumns as headers
            headers = displayColumns;
        }
        else if (headers == null && options.HasHeader && headerRow != null && columnIndices.Length > 0)
        {
            headers = columnIndices.Where(i => i < headerRow.Length)
                                 .Select(i => headerRow[i])
                                 .ToArray();
        }

        Func<string[][]> csvDataProvider = () => data.ToArray();

        return new StringArrayColumnMenuDefinitionProvider(
            loggerFactory,
            csvDataProvider,
            columnIndices,
            headers,
            new StdOutResultHandler(viewModel),
            enablePreview: !options.DisablePreview,
            viewModel: viewModel,
            allColumnHeaders: headerRow,
            displayColumns: displayColumns
        );
    }

    private static string[] ParseCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"' && !inQuotes)
            {
                inQuotes = true;
            }
            else if (c == '"' && inQuotes)
            {
                if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    current.Append('"');
                    i++; // Skip next quote
                }
                else
                {
                    inQuotes = false;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }

    private static StringArrayColumnMenuDefinitionProvider CreateProcessStringArrayColumnProvider(ILoggerFactory loggerFactory, IMainViewModel viewModel, ProcessOptions options)
    {
        // Determine sort function based on options
        Comparison<ProcessLister.ProcessInfo>? sortFunc = null;
        if (options.SortByCpu)
            sortFunc = ProcessLister.CompareProcessCpu;
        else if (options.SortByPrivateBytes)
            sortFunc = ProcessLister.CompareProcessPrivateBytes;
        else if (options.SortByWorkingSet)
            sortFunc = ProcessLister.CompareProcessWorkingSet;
        else if (options.SortByPid)
            sortFunc = ProcessLister.CompareProcessPid;

        // Create data provider that gets process data
        Func<string[][]> processDataProvider = () => ProcessLister.GetProcessesAsStringArray(
            sort: sortFunc != null,
            sortFunc: sortFunc);

        // Define column headers for processes
        var columnHeaders = new[] { "Name", "PID", "WorkingSet(kb)", "PrivateBytes(kb)", "CPU(s)" };
        var columnIndices = new[] { 0, 1, 2, 3, 4 }; // All columns

        return new StringArrayColumnMenuDefinitionProvider(
            loggerFactory,
            processDataProvider,
            columnIndices,
            columnHeaders,
            new StdOutResultHandler(viewModel),
            enablePreview: true,
            viewModel: viewModel,
            allColumnHeaders: columnHeaders,
            displayColumns: null
        );
    }
}