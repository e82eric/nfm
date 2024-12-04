using System;
using System.IO;
using System.Threading.Tasks;

namespace nfm.menu;

public class FileSystemResultHandler(
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

    public async Task HandleAsync(string output, MainViewModel viewModel)
    {
        if (!IsDirectory(output) || !searchDirectories)
        {
            await viewModel.Close();
            await fileResultHandler.HandleAsync(output, viewModel);

            if (quitAfter)
            {
                Environment.Exit(0);
            }
        }
        else
        {
            await directoryResultHandler.HandleAsync(output, viewModel);
        }
    }
}