using System.Threading;
using System.Threading.Tasks;

namespace nfm.menu;

public class CommandPreviewHandler(string commandTemplate) : IPreviewHandler
{
    public async Task Handle(IPreviewRenderer renderer, object t, CancellationToken ct)
    {
        var command = string.Format(commandTemplate, t);
        var result = await ProcessRunner.RunCommandAsync(command);
        if (result.ExitCode == 0)
        {
            renderer.RenderText(result.StandardOutput, ".txt");
        }
        else
        {
            renderer.RenderError(result.StandardOutput + "\n" + result.StandardError);
        }
    }
}