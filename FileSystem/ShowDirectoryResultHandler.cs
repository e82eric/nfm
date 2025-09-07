using nfm.Ui.Core;

namespace nfm.FileSystem;

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
        var output = outputObj.ToString();
        if (output == null)
        {
            return;
        }
        
        var definition =
            new FileSystemMenuDefinitionProvider(
                new FileSystemResultHandler(viewModel, fileResultHandler, this, quitOnEscape, true),
                Int32.MaxValue,
                [output],
                quitOnEscape,
                hasPreview,
                directoriesOnly,
                filesOnly,
                false,
                null,
                null,
                null,
                false,
                viewModel,
                null,
                onClosed).Get();

        await viewModel.Clear();
        await viewModel.RunDefinitionAsync(definition);
    }
}
