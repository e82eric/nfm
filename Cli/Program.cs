using System.Runtime.Versioning;
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
}

class CommandOptions
{
    public IEnumerable<string>? Command { get; set; }
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
                        });
                        window.Run();
                        return;
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
                });
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
}