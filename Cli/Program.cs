using System.Runtime.Versioning;
using System.Text;
using nfm.FileSystem;
using nfm.Ui.Core;
using nfm.Win32Ui;

namespace nfm.Cli;

class StdInOptions
{
    public string? Header { get; set; }
    public string? EditCommand { get; set; }
    public bool ShowPreview { get; set; }
    public string? PreviewCommand { get; set; }
    public char? Delimiter { get; set; }
    public string? PreviewStartLineCommand { get; set; }
    public string? PreviewStartLineOffsetCommand { get; set; }
    public bool ShowGap { get; set; } = false;
    public string? LineContinuation { get; set; }
    public bool WrapLines { get; set; }
    public string? SearchString { get; set; }
    public bool NoLengthSort { get; set; }
    public bool ExcludeTopmost { get; set; } = false;
}

class FileSystemOptions
{
    public bool SearchDirectoryOnSelect { get; set; } = false;
    public string RootDirectory { get; set; } = string.Empty;
    public int MaxDepth { get; set; } = int.MaxValue;
    public bool DirectoriesOnly { get; set; } = false;
    public bool FilesOnly { get; set; } = false;
    public char? Delimiter { get; set; }
    public string? PreviewStartLineCommand { get; set; }
    public string? PreviewStartLineOffsetCommand { get; set; }
    public bool WrapLines { get; set; }
    public bool ShowGap { get; set; }
    public bool ShowPreview { get; set; }
    public bool ExcludeTopmost { get; set; } = false;
}

class CommandOptions
{
    public IEnumerable<string>? Command { get; set; }
}

class CsvOptions
{
    public int[]? ColumnIndices { get; set; }
    public string[]? DisplayColumns { get; set; }
    public string[]? Headers { get; set; }
    public bool HasHeader { get; set; } = true;
    public char Delimiter { get; set; } = ',';
    public bool DisablePreview { get; set; } = false;
    public bool ExcludeTopmost { get; set; } = false;
}

[SupportedOSPlatform("windows")]
class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var viewModel = new ViewModel();
        viewModel.GlobalKeyBindings.Add((ModifierKeys.LCtl, VirtualKeyCodes.VK_C), ClipboardHelper.CopyStringToClipboard);
        viewModel.GlobalKeyBindings.Add((ModifierKeys.LCtl, VirtualKeyCodes.VK_P), (o, model) =>
        {
            viewModel.TogglePreview();
            return Task.CompletedTask;
        });
        if (Console.IsInputRedirected)
        {
            try
            {
                int nextChar = Console.In.Peek();
                if (nextChar != -1)
                {
                    // Check if this is a CSV input request
                    if (args.Length > 0 && args[0] == "csv")
                    {
                        var csvOptions = ParseCsvOptions(args);
                        if (csvOptions != null)
                        {
                            var csvProvider = CreateCsvStringArrayColumnProvider(viewModel, csvOptions);
                            var window = new Win32Window(viewModel, () =>
                            {
                                Task.Run(async () =>
                                {
                                    await viewModel.RunDefinitionAsync(csvProvider.Get());
                                });
                            }, csvOptions.ExcludeTopmost);
                            window.Run();
                            return;
                        }
                    }
                    else
                    {
                        var stdInOptions = ParseStdInOptions(args);
                        if (stdInOptions != null)
                        {
                            var menuDefinitionProvider = new StdInMenuDefinitionProvider(
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
                            var window = new Win32Window(viewModel,() =>
                            {
                                Task.Run(async () =>
                                {
                                    await viewModel.RunDefinitionAsync(menuDefinitionProvider.Get());
                                });
                            }, stdInOptions.ExcludeTopmost);
                            window.Run();
                            return;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Handle exception
            }
        }
        
        if (args.Length > 0)
        {
            if (args[0] == "filesystem")
            {
                var fileSystemOptions = ParseFileSystemOptions(args);
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
                    null);
                var window = new Win32Window(viewModel,() =>
                {
                    Task.Run(async () =>
                    {
                        await viewModel.RunDefinitionAsync(definitionProvider.Get());
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
                    return;
                }
            }
        }
    }
    
    private static StdInOptions? ParseStdInOptions(string[] args)
    {
        var options = new StdInOptions();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--header" && i + 1 < args.Length)
            {
                options.Header = args[i + 1];
                i++;
            }
            else if (args[i] == "--editcommand" && i + 1 < args.Length)
            {
                options.EditCommand = args[i + 1];
                i++;
            }
            else if (args[i] == "--previewcommand" && i + 1 < args.Length)
            {
                options.PreviewCommand = args[i + 1];
                i++;
            }
            else if (args[i] == "--searchstring" && i + 1 < args.Length)
            {
                options.SearchString = args[i + 1];
                i++;
            }
            else if (args[i] == "--previewstartlinecommand" && i + 1 < args.Length)
            {
                options.PreviewStartLineCommand = args[i + 1];
                i++;
            }
            else if (args[i] == "--previewstartlineoffsetcommand" && i + 1 < args.Length)
            {
                options.PreviewStartLineOffsetCommand = args[i + 1];
                i++;
            }
            else if (args[i] == "--delimiter" && i + 1 < args.Length)
            {
                if (args[i + 1].Length == 1)
                {
                    options.Delimiter = args[i + 1][0];
                }
                i++;
            }
            else if (args[i] == "--gap")
            {
                options.ShowGap = true;
            }
            else if (args[i] == "--showpreview")
            {
                options.ShowPreview = true;
            }
            else if (args[i] == "--nolengthsort")
            {
                options.NoLengthSort = true;
            }
            else if (args[i] == "--wrap")
            {
                options.WrapLines = true;
            }
            else if (args[i] == "--linecontinuation")
            {
                if (args[i + 1].Length == 1)
                {
                    options.LineContinuation = args[i + 1];
                }

                i++;
            }
            else if (args[i] == "--exclude-topmost" || args[i] == "--no-topmost")
            {
                options.ExcludeTopmost = true;
            }
            else
            {
                Console.Error.WriteLine($"Unknown argument: {args[i]}");
                return null;
            }
        }
        return options;
    }
    
    private static FileSystemOptions? ParseFileSystemOptions(string[] args)
    {
        var options = new FileSystemOptions();
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--searchdirectoryonselect")
            {
                options.SearchDirectoryOnSelect = true;
            }
            else if (args[i] == "--rootdirectory" && i + 1 < args.Length)
            {
                options.RootDirectory = args[i + 1];
                i++;
            }
            else if (args[i] == "--maxdepth" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int maxDepth) && maxDepth > 0)
                {
                    options.MaxDepth = maxDepth;
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
                options.ShowPreview = true;
            }
            else if (args[i] == "--directoriesonly")
            {
                options.DirectoriesOnly = true;
            }
            else if (args[i] == "--filesonly")
            {
                options.FilesOnly = true;
            }
            else if (args[i] == "--wrap")
            {
                options.WrapLines = true;
            }
            else if (args[i] == "--gap")
            {
                options.ShowGap = true;
            }
            else if (args[i] == "--previewstartlinecommand" && i + 1 < args.Length)
            {
                options.PreviewStartLineCommand = args[i + 1];
                i++;
            }
            else if (args[i] == "--previewstartlineoffsetcommand" && i + 1 < args.Length)
            {
                options.PreviewStartLineOffsetCommand = args[i + 1];
                i++;
            }
            else if (args[i] == "--delimiter" && i + 1 < args.Length)
            {
                if (args[i + 1].Length == 1)
                {
                    options.Delimiter = args[i + 1][0];
                }
                i++;
            }
            else if (args[i] == "--exclude-topmost" || args[i] == "--no-topmost")
            {
                options.ExcludeTopmost = true;
            }
            else
            {
                Console.Error.WriteLine($"Unknown argument: {args[i]}");
                return null;
            }
        }

        if (options.DirectoriesOnly && options.FilesOnly)
        {
            Console.Error.WriteLine("Error: --directoriesonly and --filesonly are mutually exclusive.");
            return null;
        }

        if (string.IsNullOrEmpty(options.RootDirectory))
        {
            options.RootDirectory = Directory.GetCurrentDirectory();
        }

        if (!Directory.Exists(options.RootDirectory))
        {
            Console.Error.WriteLine($"Error: Directory not found: {options.RootDirectory}");
            return null;
        }

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
        var options = new CommandOptions
        {
            Command = command
        };
        return options;
    }

    private static CsvOptions? ParseCsvOptions(string[] args)
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
                    options.ColumnIndices = columnIndices.ToArray();
                }
                if (displayColumns.Count > 0)
                {
                    options.DisplayColumns = displayColumns.ToArray();
                }
                i++;
            }
            else if (args[i] == "--headers" && i + 1 < args.Length)
            {
                options.Headers = args[i + 1].Split(',');
                i++;
            }
            else if (args[i] == "--delimiter" && i + 1 < args.Length)
            {
                if (args[i + 1].Length == 1)
                {
                    options.Delimiter = args[i + 1][0];
                }
                i++;
            }
            else if (args[i] == "--no-header")
            {
                options.HasHeader = false;
            }
            else if (args[i] == "--disable-preview")
            {
                options.DisablePreview = true;
            }
            else if (args[i] == "--exclude-topmost" || args[i] == "--no-topmost")
            {
                options.ExcludeTopmost = true;
            }
            else
            {
                Console.Error.WriteLine($"Unknown CSV argument: {args[i]}");
                return null;
            }
        }
        return options;
    }

    private static StringArrayColumnMenuDefinitionProvider CreateCsvStringArrayColumnProvider(IMainViewModel viewModel, CsvOptions options)
    {
        // Read CSV input once
        var csvInput = Console.In.ReadToEnd();
        if (string.IsNullOrWhiteSpace(csvInput))
        {
            Console.Error.WriteLine("No CSV input provided");
            return new StringArrayColumnMenuDefinitionProvider(
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
}