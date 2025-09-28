using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using nfm.Ui.Core;
using nfzf;

namespace nfm.ListProcesses;

public class ShowProcessesMenuDefinitionProvider(IMainViewModel mainViewModel, Action? onClosed, IResultHandler? resultHandler = null) : IMenuDefinitionProvider
{
    private static readonly string[] ColumnHeaders = ["Name", "PID", "WorkingSet", "PrivateBytes", "CPU"];
    private static readonly int[] ColumnIndices = [0, 1, 2, 3, 4];

    public MenuDefinition Get()
    {
        var provider = new StringArrayColumnMenuDefinitionProvider(
            () => ProcessLister.GetProcessesAsStringArray(true, ProcessLister.CompareProcessWorkingSet),
            ColumnIndices,
            ColumnHeaders,
            resultHandler ?? new ProcessResultHandler(),
            enablePreview: true,
            viewModel: mainViewModel,
            allColumnHeaders: ColumnHeaders,
            quitOnEscape: false,
            onClosed: onClosed
        );

        var definition = provider.Get();

        return definition;
    }
}

public class ProcessResultHandler : IResultHandler
{
    public Task HandleAsync(object objOutput)
    {
        if (objOutput is StringArrayRow row)
        {
            var processName = row.GetAllData()[0];
            var pid = row.GetAllData()[1];
            Console.WriteLine($"{processName} (PID: {pid})");
        }
        else
        {
            Console.WriteLine(objOutput?.ToString() ?? string.Empty);
        }
        return Task.CompletedTask;
    }
}