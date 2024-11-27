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
    bool hasPreview,
    bool directoriesOnly,
    bool filesOnly,
    MainViewModel viewModel,
    IComparer<Entry>? comparer) : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = rootDirectory == null || !rootDirectory.Any() ?
                (writer, ct) => ListDrives(maxDepth, writer, ct) :
                (writer, ct) => ListDrives(rootDirectory, maxDepth, writer, ct),
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Func<string, Task>>(),
            MinScore = 0,
            ResultHandler = resultHandler,
            ShowHeader = false,
            QuitOnEscape = quitOnEscape,
            HasPreview = hasPreview,
            Comparer = comparer
        };
        definition.KeyBindings.Add((KeyModifiers.Control, Key.O), _ => ParentDir(rootDirectory));
        return definition;
    }

    private async Task ParentDir(IEnumerable<string>? dirs)
    {
        if (dirs != null && dirs.Any())
        {
            var first = dirs.First();
            var directoryInfo = new DirectoryInfo(first);
            var parent = directoryInfo.Parent;
            var definition = new FileSystemMenuDefinitionProvider(resultHandler, maxDepth, [parent.FullName],
                quitOnEscape, hasPreview, directoriesOnly, filesOnly, viewModel, null).Get();

            await viewModel.Clear();
            await viewModel.RunDefinitionAsync(definition);
        }
    }
    
    private async Task ListDrives(int maxDepth, ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        DriveInfo[] allDrives = DriveInfo.GetDrives();
        var drives = allDrives.Select(d => d.Name);
        await ListDrives(drives, maxDepth, writer, cancellationToken);
    }
    
    private async Task ListDrives(IEnumerable<string> rootDirectories, int maxDepth, ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        var fileScanner = new FileWalker();
        await fileScanner.StartScanForDirectoriesAsync(rootDirectories, writer, maxDepth, directoriesOnly, filesOnly, cancellationToken);
    }
}