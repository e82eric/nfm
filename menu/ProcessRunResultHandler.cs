using System.Diagnostics;
using System.Threading.Tasks;

namespace nfm.menu;

public class ProcessRunResultHandler : IResultHandler
{
    public void Handle(string output)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = output,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }

    public Task HandleAsync(string output)
    {
        Handle(output);
        return Task.CompletedTask;
    }
}