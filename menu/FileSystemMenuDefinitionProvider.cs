using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Input;
using nfzf.FileSystem;

namespace nfm.menu;

public class FileSystemMenuDefinitionProvider(
    TestResultHandler resultHandler,
    int maxDepth,
    IEnumerable<string>? rootDirectory,
    bool quitOnEscape,
    bool hasPreview) : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = rootDirectory == null || !rootDirectory.Any() ?
                (writer, ct) => ListDrives(maxDepth, writer, ct) :
                (writer, ct) => ListDrives(rootDirectory, maxDepth, writer, ct),
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
            MinScore = 0,
            ResultHandler = resultHandler,
            ShowHeader = false,
            QuitOnEscape = quitOnEscape,
            HasPreview = hasPreview
        };
        return definition;
    }
    
    private async Task ListDrives(int maxDepth, ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        DriveInfo[] allDrives = DriveInfo.GetDrives();
        var drives = allDrives.Select(d => d.Name);
        await ListDrives(drives, maxDepth, writer, cancellationToken);
    }
    
    private async Task ListDrives(IEnumerable<string> rootDirectories, int maxDepth, ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        var fileScanner = new StreamingWin32DriveScanner2();
        await fileScanner.StartScanForDirectoriesAsync(rootDirectories, writer, maxDepth, cancellationToken);
    }
}