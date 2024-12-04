using System.Diagnostics;
using System.Threading.Tasks;

namespace nfm.menu;

public class RunFileResultHandler : IResultHandler
{
    public async Task HandleAsync(string output, MainViewModel viewModel)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = output,
            UseShellExecute = true
        };
        await Task.Run(() => Process.Start(startInfo));
    }
}