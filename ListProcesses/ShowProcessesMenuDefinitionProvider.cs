using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using nfm.Ui.Core;
using nfzf;

namespace nfm.ListProcesses;

public class ShowProcessesMenuDefinitionProvider(IMainViewModel mainViewModel, Action? onClosed) : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var header = string.Format("{0,-75} {1,8} {2,20} {3,20} {4,10}",
            "Name", "PID", "WorkingSet(kb)", "PrivateBytes(kb)", "CPU(s)");
        var definition = CreateDefinition(ProcessLister.RunSortedByWorkingSet2, header ,Comparers.ScoreLengthAndValue);
        definition.KeyBindings.Add((ModifierKeys.LCtl, VirtualKeyCodes.VK_K), async lineObj =>
        {
            var line = (string)lineObj;
            var match = Regex.Match(line, @"\s+([0-9]+)\s+");
            if (!match.Success)
            {
                return;
            }

            var pidString = match.Groups[1].Value;

            var pid = Convert.ToInt32(pidString);
            
            await mainViewModel.ShowToast($"Killed process {pid}", 500);
            await ProcessLister.KillProcessById(line, pid);
        });
        definition.KeyBindings.Add((ModifierKeys.LCtl, VirtualKeyCodes.VK_M), async lineObj =>
        {
            var line = (string)lineObj;
            var match = Regex.Match(line, @"\s+([0-9]+)\s+");
            if (!match.Success)
            {
                await mainViewModel.ShowToast("Failed to parse process ID from input.");
                return;
            }

            if (!int.TryParse(match.Groups[1].Value, out var pid))
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
        });

        AddResultKeyBinding(definition.KeyBindings, header, ProcessLister.RunSortedByCpu2, (ModifierKeys.LCtl, VirtualKeyCodes.VK_F1));
        AddResultKeyBinding(definition.KeyBindings, header, ProcessLister.RunSortedByPid2, (ModifierKeys.LCtl, VirtualKeyCodes.VK_F2));
        AddResultKeyBinding(definition.KeyBindings, header, ProcessLister.RunSortedByPrivateBytes2, (ModifierKeys.LCtl, VirtualKeyCodes.VK_F3));
        AddResultKeyBinding(definition.KeyBindings, header, ProcessLister.RunSortedByWorkingSet2, (ModifierKeys.LCtl, VirtualKeyCodes.VK_F4));

        return definition;
    }

    private void AddResultKeyBinding(
        Dictionary<(ModifierKeys, int), Func<object, Task>> keyBindings,
        string header,
        Func<ChannelWriter<object>, CancellationToken, Task> asyncFunc,
        (ModifierKeys Control, int D4) keys)
    {
        keyBindings.Add(keys, async _ =>
        {
            var definition = CreateDefinition(asyncFunc, header, Comparer);
            await mainViewModel.Clear();
            await mainViewModel.RunDefinitionAsync(definition);
        });
    }

    private MenuDefinition CreateDefinition(
        Func<ChannelWriter<object>, CancellationToken, Task> resultFunc,
        string? header,
        IComparer<Entry> comparer)
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = resultFunc,
            Header = header,
            MinScore = 0,
            ResultHandler = new StdOutResultHandler(mainViewModel),
            Comparer = comparer,
            FinalComparer = comparer,
            OnClosed = onClosed,
            ScoreFunc = (sObj, pattern, slab) =>
            {
                var s = (string)sObj;
                var result = FuzzySearcher.GetScore(s, pattern, slab);
                return (s.Length, result);
            },
        };
        return definition;
    }

    private static readonly IComparer<Entry> Comparer = Comparer<Entry>.Create((x, y) => y.Score.CompareTo(x.Score));
}