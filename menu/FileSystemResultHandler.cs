using System;
using System.IO;
using System.Threading.Tasks;
using nfzf.FileSystem;

namespace nfm.menu;

public class FileSystemResultHandler(
    IResultHandler<FileSystemNode> fileResultHandler,
    IResultHandler<FileSystemNode> directoryResultHandler,
    bool quitAfter,
    bool searchDirectories)
    : IResultHandler<FileSystemNode>
{
    private static bool IsDirectory(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        return attributes.HasFlag(FileAttributes.Directory);
    }

    public async Task HandleAsync(FileSystemNode output, MainViewModel<FileSystemNode> viewModel)
    {
        var path = output.ToString();
        if (!IsDirectory(path) || !searchDirectories)
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