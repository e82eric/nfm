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
            allColumnHeaders: ColumnHeaders
        );

        var definition = provider.Get();

        // Create a new definition with OnClosed callback
        var newDefinition = new MenuDefinition
        {
            AsyncFunction = definition.AsyncFunction,
            Header = definition.Header,
            HasPreview = definition.HasPreview,
            PreviewHandler = definition.PreviewHandler,
            ResultHandler = definition.ResultHandler,
            MinScore = definition.MinScore,
            QuitOnEscape = definition.QuitOnEscape,
            ScoreFunc = definition.ScoreFunc,
            PreFilter = definition.PreFilter,
            ShowGap = definition.ShowGap,
            Wrap = definition.Wrap,
            SearchString = definition.SearchString,
            PreParseFunc = definition.PreParseFunc,
            AutoCompleteProvider = definition.AutoCompleteProvider,
            OnClosed = onClosed
        };

        // Copy existing key bindings
        foreach (var binding in definition.KeyBindings)
        {
            newDefinition.KeyBindings[binding.Key] = binding.Value;
        }

        // Add process-specific key bindings
        newDefinition.KeyBindings.Add((ModifierKeys.LCtl, VirtualKeyCodes.VK_K), async lineObj =>
        {
            if (lineObj is StringArrayRow row)
            {
                var pidStr = row.GetAllData()[1]; // PID is in column 1
                if (int.TryParse(pidStr, out var pid))
                {
                    await mainViewModel.ShowToast($"Killed process {pid}", 500);
                    await ProcessLister.KillProcessById("", pid);
                }
            }
        });

        newDefinition.KeyBindings.Add((ModifierKeys.LCtl, VirtualKeyCodes.VK_M), async lineObj =>
        {
            if (lineObj is StringArrayRow row)
            {
                var pidStr = row.GetAllData()[1]; // PID is in column 1
                if (!int.TryParse(pidStr, out var pid))
                {
                    await mainViewModel.ShowToast("Invalid process ID format.");
                    return;
                }

                await Task.Run(async () =>
                {
                    try
                    {
                        var process = Process.GetProcessById(pid);
                        var dumpFilePath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            $"{process.ProcessName}_{DateTimeOffset.Now:yyyyMMddHHmmss}.dmp");

                        MemoryDumpTaker.TakeMemoryDump(pid, dumpFilePath);
                        await mainViewModel.ShowToast($"Memory dump of {process.ProcessName} saved to: {dumpFilePath}");
                    }
                    catch (Exception e)
                    {
                        await mainViewModel.ShowToast($"Memory dump of {pid} failed: {e.Message}");
                    }
                });
            }
        });

        // Add sort key bindings
        newDefinition.KeyBindings.Add((ModifierKeys.LCtl, VirtualKeyCodes.VK_F1), async _ =>
        {
            var newProvider = new StringArrayColumnMenuDefinitionProvider(
                () => ProcessLister.GetProcessesAsStringArray(true, ProcessLister.CompareProcessCpu),
                ColumnIndices, ColumnHeaders, resultHandler ?? new ProcessResultHandler(), true, mainViewModel, ColumnHeaders);
            var sortDefinition = newProvider.Get();
            var finalDefinition = CopyKeyBindings(newDefinition, sortDefinition);
            await mainViewModel.Clear();
            await mainViewModel.RunDefinitionAsync(finalDefinition);
        });

        newDefinition.KeyBindings.Add((ModifierKeys.LCtl, VirtualKeyCodes.VK_F2), async _ =>
        {
            var newProvider = new StringArrayColumnMenuDefinitionProvider(
                () => ProcessLister.GetProcessesAsStringArray(true, ProcessLister.CompareProcessPid),
                ColumnIndices, ColumnHeaders, resultHandler ?? new ProcessResultHandler(), true, mainViewModel, ColumnHeaders);
            var sortDefinition = newProvider.Get();
            var finalDefinition = CopyKeyBindings(newDefinition, sortDefinition);
            await mainViewModel.Clear();
            await mainViewModel.RunDefinitionAsync(finalDefinition);
        });

        newDefinition.KeyBindings.Add((ModifierKeys.LCtl, VirtualKeyCodes.VK_F3), async _ =>
        {
            var newProvider = new StringArrayColumnMenuDefinitionProvider(
                () => ProcessLister.GetProcessesAsStringArray(true, ProcessLister.CompareProcessPrivateBytes),
                ColumnIndices, ColumnHeaders, resultHandler ?? new ProcessResultHandler(), true, mainViewModel, ColumnHeaders);
            var sortDefinition = newProvider.Get();
            var finalDefinition = CopyKeyBindings(newDefinition, sortDefinition);
            await mainViewModel.Clear();
            await mainViewModel.RunDefinitionAsync(finalDefinition);
        });

        newDefinition.KeyBindings.Add((ModifierKeys.LCtl, VirtualKeyCodes.VK_F4), async _ =>
        {
            var newProvider = new StringArrayColumnMenuDefinitionProvider(
                () => ProcessLister.GetProcessesAsStringArray(true, ProcessLister.CompareProcessWorkingSet),
                ColumnIndices, ColumnHeaders, resultHandler ?? new ProcessResultHandler(), true, mainViewModel, ColumnHeaders);
            var sortDefinition = newProvider.Get();
            var finalDefinition = CopyKeyBindings(newDefinition, sortDefinition);
            await mainViewModel.Clear();
            await mainViewModel.RunDefinitionAsync(finalDefinition);
        });

        return newDefinition;
    }

    private static MenuDefinition CopyKeyBindings(MenuDefinition source, MenuDefinition target)
    {
        // Create a new definition with OnClosed callback
        var newDefinition = new MenuDefinition
        {
            AsyncFunction = target.AsyncFunction,
            Header = target.Header,
            HasPreview = target.HasPreview,
            PreviewHandler = target.PreviewHandler,
            ResultHandler = target.ResultHandler,
            MinScore = target.MinScore,
            QuitOnEscape = target.QuitOnEscape,
            ScoreFunc = target.ScoreFunc,
            PreFilter = target.PreFilter,
            ShowGap = target.ShowGap,
            Wrap = target.Wrap,
            SearchString = target.SearchString,
            PreParseFunc = target.PreParseFunc,
            AutoCompleteProvider = target.AutoCompleteProvider,
            OnClosed = source.OnClosed
        };

        // Copy existing key bindings from target
        foreach (var binding in target.KeyBindings)
        {
            newDefinition.KeyBindings[binding.Key] = binding.Value;
        }

        // Copy the process-specific key bindings from source
        foreach (var binding in source.KeyBindings)
        {
            if (binding.Key.Item1 == ModifierKeys.LCtl &&
                (binding.Key.Item2 == VirtualKeyCodes.VK_K || binding.Key.Item2 == VirtualKeyCodes.VK_M))
            {
                newDefinition.KeyBindings[binding.Key] = binding.Value;
            }
        }

        return newDefinition;
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