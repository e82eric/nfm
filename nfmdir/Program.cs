using System.Threading.Channels;
using nfm.FileWalker;

namespace nfm.NfmDir;

internal sealed record Options(
    string RootDirectory,
    bool IncludeFiles
);

class Program
{
    static async Task<int> Main(string[] args)
    {
        var options = ParseOptions(args);
        if (options is null)
        {
            PrintUsage();
            return 1;
        }

        if (!Directory.Exists(options.RootDirectory))
        {
            Console.Error.WriteLine($"Error: directory not found: {options.RootDirectory}");
            return 1;
        }

        var channel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var walker = new nfm.FileWalker.FileWalker();
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var scanTask = walker.StartScanForDirectoriesAsync(
            new[] { options.RootDirectory },
            channel.Writer,
            maxDepth: int.MaxValue,
            directoriesOnly: !options.IncludeFiles,
            filesOnly: false,
            cts.Token);

        var stdout = Console.Out;
        await foreach (var item in channel.Reader.ReadAllAsync(cts.Token))
        {
            if (item is FileSystemNode node)
            {
                stdout.WriteLine(node.ToString());
            }
        }

        await scanTask;
        return 0;
    }

    private static Options? ParseOptions(string[] args)
    {
        string? root = null;
        bool includeFiles = false;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a == "--include-files" || a == "-f")
            {
                includeFiles = true;
            }
            else if (a == "--help" || a == "-h")
            {
                return null;
            }
            else if (!a.StartsWith("-") && root is null)
            {
                root = a;
            }
            else
            {
                Console.Error.WriteLine($"Unknown argument: {a}");
                return null;
            }
        }

        if (root is null)
        {
            root = Directory.GetCurrentDirectory();
        }

        return new Options(root, includeFiles);
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: filewalker [directory] [--include-files|-f]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  directory         starting directory (defaults to current directory)");
        Console.Error.WriteLine("  --include-files   include files in output (directories only by default)");
    }
}
