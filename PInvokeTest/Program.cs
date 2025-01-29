using nfm.menu;
using Win32FromForms;

class StdInOptions
{
    public string? Header { get; set; }
    public string? EditCommand { get; set; }
    public string? PreviewCommand { get; set; }
}

class FileSystemOptions
{
    public bool SearchDirectoryOnSelect { get; set; } = false;
    public string RootDirectory { get; set; } = string.Empty;
    public int MaxDepth { get; set; } = int.MaxValue;
    public bool HasPreview { get; set; } = false;
    public bool DirectoriesOnly { get; set; } = false;
    public bool FilesOnly { get; set; } = false;
}

class CommandOptions
{
    public IEnumerable<string>? Command { get; set; }
}

class FileReaderOptions
{
    public string? Path { get; set; }
    public string? SearchString { get; set; }
}

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
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
                        var viewModel = new ViewModel();
                        var menuDefinitionProvider = new StdInMenuDefinitionProvider(
                            viewModel,
                            false,
                            null,
                            null,
                            null);
                        var window = new Win32Window();
                        window.Create(viewModel,() =>
                        {
                             _ =viewModel.RunDefinitionAsync(menuDefinitionProvider.Get()); 
                        });
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
                var viewModel = new ViewModel();
                var definitionProvider = new FileSystemMenuDefinitionProvider(
                    new StdOutResultHandler(viewModel),
                    fileSystemOptions.MaxDepth,
                    [fileSystemOptions.RootDirectory],
                    true,
                    false,
                    fileSystemOptions.DirectoriesOnly,
                    fileSystemOptions.FilesOnly,
                    viewModel,
                    null,
                    null);
                var window = new Win32Window();
                window.Create(viewModel, () =>
                {
                    _ = viewModel.RunDefinitionAsync(definitionProvider.Get());
                });
            }
            else if (args[0] == "command")
            {
                var commandOptions = ParseCommandOptions(args);
                if (commandOptions != null && commandOptions.Command != null)
                {
                    //BuildCommandApp(string.Join(" ", commandOptions.Command))
                    //    .Start((application, strings) => Run(application, false), args);
                    return;
                }
            }
            else if (args[0] == "filereader")
            {
                var fileReaderOptions = ParseFileReaderOptions(args);
                if (fileReaderOptions != null && fileReaderOptions.Path != null)
                {
                    var searchString = fileReaderOptions.SearchString ?? string.Empty;
                    var viewModel = new ViewModel();
                    var window = new Win32Window();
                    window.Create(viewModel, () =>
                    {
                        var definitionProvider = new ReadFileMenuDefinitionProvider(
                            fileReaderOptions.Path,
                            Comparers.ScoreOnly, 
                            searchString,
                            viewModel);
                        var definition = definitionProvider.Get();
                        _ = viewModel.RunDefinitionAsync(definition);
                    });
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
            else if (args[i] == "--edit-command" && i + 1 < args.Length)
            {
                options.EditCommand = args[i + 1];
                i++;
            }
            else if (args[i] == "--preview-command" && i + 1 < args.Length)
            {
                options.PreviewCommand = args[i + 1];
                i++;
            }
        }
        return options;
    }
    
    private static FileSystemOptions ParseFileSystemOptions(string[] args)
    {
        var options = new FileSystemOptions();
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--search-directory-on-select")
            {
                options.SearchDirectoryOnSelect = true;
            }
            else if (args[i] == "--root-directory" && i + 1 < args.Length)
            {
                options.RootDirectory = args[i + 1];
                i++;
            }
            else if (args[i] == "--max-depth" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int maxDepth))
                {
                    options.MaxDepth = maxDepth;
                    i++;
                }
            }
            else if (args[i] == "--has-preview")
            {
                options.HasPreview = true;
            }
            else if (args[i] == "--directories-only")
            {
                options.DirectoriesOnly = true;
            }
            else if (args[i] == "--files-only")
            {
                options.FilesOnly = true;
            }
        }
        return options;
    }
    
    private static CommandOptions? ParseCommandOptions(string[] args)
    {
        var options = new CommandOptions();
        options.Command = args.Skip(1).ToList();
        return options;
    }

    private static FileReaderOptions? ParseFileReaderOptions(string[] args)
    {
        var options = new FileReaderOptions();
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--path" && i + 1 < args.Length)
            {
                options.Path = args[i + 1];
                i++;
            }
            else if (args[i] == "--search-string" && i + 1 < args.Length)
            {
                options.SearchString = args[i + 1];
                i++;
            }
        }
        return options;
    }
}