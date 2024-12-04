using System;
using System.Threading.Tasks;

namespace nfm.menu;

public class ShowDirectoryResultHandler(
    IResultHandler fileResultHandler,
    bool quitOnEscape,
    bool hasPreview,
    bool directoriesOnly,
    bool filesOnly,
    Action? onClosed,
    string? title) : IResultHandler
{
    public async Task HandleAsync(string output, MainViewModel viewModel)
    {
        var definition =
            new FileSystemMenuDefinitionProvider(
                new FileSystemResultHandler(fileResultHandler, this, quitOnEscape, true),
                Int32.MaxValue,
                [output],
                quitOnEscape,
                hasPreview,
                directoriesOnly,
                filesOnly,
                viewModel,
                null,
                onClosed,
                title).Get();

        await viewModel.Clear();
        await viewModel.RunDefinitionAsync(definition);
    }
}