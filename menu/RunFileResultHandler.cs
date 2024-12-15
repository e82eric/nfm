using System.Diagnostics;
using System.Threading.Tasks;
using nfzf.FileSystem;

namespace nfm.menu;

public class RunFileResultHandler : IResultHandler
{
    public async Task HandleAsync(object outputObj, MainViewModel viewModel)
    {
        var output = (FileSystemNode)outputObj;
        var startInfo = new ProcessStartInfo
        {
            FileName = output.ToString(),
            UseShellExecute = true
        };
        await Task.Run(() => Process.Start(startInfo));
    }
}
