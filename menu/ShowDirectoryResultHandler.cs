using System;
using System.Threading.Tasks;
using nfzf.FileSystem;

namespace nfm.menu;

public class ShowDirectoryResultHandler(
    IResultHandler<FileSystemNode> fileResultHandler,
    bool quitOnEscape,
    bool hasPreview,
    bool directoriesOnly,
    bool filesOnly,
    Action? onClosed) : IResultHandler<FileSystemNode>
{
    public async Task HandleAsync(FileSystemNode output, MainViewModel<FileSystemNode> viewModel)
    {
        var definition =
            new FileSystemMenuDefinitionProvider(
                new FileSystemResultHandler(fileResultHandler, this, quitOnEscape, true),
                Int32.MaxValue,
                [output.ToString()],
                quitOnEscape,
                hasPreview,
                directoriesOnly,
                filesOnly,
                viewModel,
                null,
                onClosed).Get();

        await viewModel.Clear();
        await viewModel.RunDefinitionAsync(definition);
    }
}