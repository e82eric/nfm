using System.Diagnostics;

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
}