using System.Diagnostics;
using System.Threading.Tasks;

namespace nfm.menu;

public class ProcessRunResultHandler : IResultHandler
{
    private void Handle(string output)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = output,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }

    public Task HandleAsync(string output, MainViewModel viewModel)
    {
        viewModel.Close();
        Handle(output);
        return Task.CompletedTask;
    }
}