using System.Diagnostics;
using System.Threading.Tasks;
using nfzf.FileSystem;

namespace nfm.menu;

public class RunFileResultHandler : IResultHandler<FileSystemNode>
{
    public async Task HandleAsync(FileSystemNode output, MainViewModel<FileSystemNode> viewModel)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = output.ToString(),
            UseShellExecute = true
        };
        await Task.Run(() => Process.Start(startInfo));
    }
}