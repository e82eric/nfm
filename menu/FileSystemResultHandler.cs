using System;
using System.IO;
using System.Threading.Tasks;
using nfzf.FileSystem;

namespace nfm.menu;

public class FileSystemResultHandler(
    MainViewModel viewModel,
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
        var output = (FileSystemNode)outputObj;
        var path = output.ToString();
        if (!IsDirectory(path) || !searchDirectories)
        {
            await viewModel.Close();
            await fileResultHandler.HandleAsync(output);

            if (quitAfter)
            {
                Environment.Exit(0);
            }
        }
        else
        {
            await directoryResultHandler.HandleAsync(output);
        }
    }
}
