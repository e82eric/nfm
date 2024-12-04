using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Input;
using nfzf.ListProcesses;

namespace nfm.menu;

public class ShowProcessesMenuDefinitionProvider(MainViewModel mainViewModel, Action? onClosed) : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var header = string.Format("{0,-75} {1,8} {2,20} {3,20} {4,10}",
            "Name", "PID", "WorkingSet(kb)", "PrivateBytes(kb)", "CPU(s)");
        var keyBindings = new Dictionary<(KeyModifiers, Key), Func<string, Task>>();
        keyBindings.Add((KeyModifiers.Control, Key.K), async line =>
        {
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
        keyBindings.Add((KeyModifiers.Control, Key.C), ClipboardHelper.CopyStringToClipboard);
        keyBindings.Add((KeyModifiers.Control, Key.D1), async _ =>
        {
            var definition = new MenuDefinition
            {
                AsyncFunction = ProcessLister.RunSortedByCpu,
                Header = header,
                KeyBindings = keyBindings,
                MinScore = 0,
                ResultHandler = new StdOutResultHandler(),
                ShowHeader = true,
                Comparer = Comparer,
                OnClosed = onClosed,
                Title = "Processes"
            };

            await mainViewModel.Clear();
            await mainViewModel.RunDefinitionAsync(definition);
        });
        keyBindings.Add((KeyModifiers.Control, Key.D2), async _ =>
        {
            var definition = new MenuDefinition
            {
                AsyncFunction = ProcessLister.RunSortedByPid,
                Header = header,
                KeyBindings = keyBindings,
                MinScore = 0,
                ResultHandler = new StdOutResultHandler(),
                ShowHeader = true,
                Comparer = Comparer,
                OnClosed = onClosed,
                Title = "Processes"
            };

            await mainViewModel.Clear();
            await mainViewModel.RunDefinitionAsync(definition);
        });
        keyBindings.Add((KeyModifiers.Control, Key.D3), async _ =>
        {
            var definition = new MenuDefinition
            {
                AsyncFunction = ProcessLister.RunSortedByPrivateBytes,
                Header = header,
                KeyBindings = keyBindings,
                MinScore = 0,
                ResultHandler = new StdOutResultHandler(),
                ShowHeader = true,
                Comparer = Comparer,
                OnClosed = onClosed,
                Title = "Processes"
            };

            await mainViewModel.Clear();
            await mainViewModel.RunDefinitionAsync(definition);
        });
        keyBindings.Add((KeyModifiers.Control, Key.D4), async _ =>
        {
            var definition = new MenuDefinition
            {
                AsyncFunction = ProcessLister.RunSortedByWorkingSet,
                Header = header,
                KeyBindings = keyBindings,
                MinScore = 0,
                ResultHandler = new StdOutResultHandler(),
                ShowHeader = true,
                Comparer = Comparer,
                OnClosed = onClosed,
                Title = "Processes"
            };

            await mainViewModel.Clear();
            await mainViewModel.RunDefinitionAsync(definition);
        });

        var definition = new MenuDefinition
        {
            AsyncFunction = ProcessLister.RunNoSort,
            Header = header,
            KeyBindings = keyBindings,
            MinScore = 0,
            ResultHandler = new StdOutResultHandler(),
            ShowHeader = true,
            OnClosed = onClosed,
            Title = "Processes"
        };
        return definition;
    }
    
    private static readonly IComparer<Entry> Comparer = Comparer<Entry>.Create((x, y) => y.Score.CompareTo(x.Score));
}