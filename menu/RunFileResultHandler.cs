using System.Diagnostics;
using System.Threading.Tasks;
using nfzf.FileSystem;

namespace nfm.menu;

public class RunFileResultHandler : IResultHandler
{
    public async Task HandleAsync(object outputObj)
    {
        var output = outputObj.ToString();
        var startInfo = new ProcessStartInfo
        {
            FileName = output,
            UseShellExecute = true
        };
        await Task.Run(() => Process.Start(startInfo));
    }
}
