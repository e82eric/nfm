using nfzf.FileSystem;

namespace nfm.menu;

public class ShowDirectoryResultHandler(
    IMainViewModel viewModel,
    IResultHandler fileResultHandler,
    bool quitOnEscape,
    bool hasPreview,
    bool directoriesOnly,
    bool filesOnly,
    Action? onClosed) : IResultHandler
{
    public async Task HandleAsync(object outputObj)
    {
        var output = (FileSystemNode)outputObj;
        var definition =
            new FileSystemMenuDefinitionProvider(
                new FileSystemResultHandler(viewModel, fileResultHandler, this, quitOnEscape, true),
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
