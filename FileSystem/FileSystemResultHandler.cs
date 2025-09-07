using nfm.Ui.Core;

namespace nfm.FileSystem;

public class FileSystemResultHandler(
    IMainViewModel viewModel,
    IResultHandler fileResultHandler,
    IResultHandler directoryResultHandler,
    bool quitAfter,
    bool searchDirectories)
    : IResultHandler
{
    private static bool IsDirectory(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        return attributes.HasFlag(FileAttributes.Directory);
    }

    public async Task HandleAsync(object outputObj)
    {
        var path = outputObj.ToString();
        if (path == null)
        {
            return;
        }
        
        if (!IsDirectory(path) || !searchDirectories)
        {
            await viewModel.Close(quitAfter);
            await fileResultHandler.HandleAsync(path);

            if (quitAfter)
            {
                Environment.Exit(0);
            }
        }
        else
        {
            await directoryResultHandler.HandleAsync(path);
        }
    }
}
