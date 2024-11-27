using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Input;
using nfzf.ListProcesses;

namespace nfm.menu;

public class ShowProcessesMenuDefinitionProvider(MainViewModel mainViewModel) : IMenuDefinitionProvider
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

        var definition = new MenuDefinition
        {
            AsyncFunction = ProcessLister.RunNoSort,
            Header = header,
            KeyBindings = keyBindings,
            MinScore = 0,
            ResultHandler = new StdOutResultHandler(),
            ShowHeader = true
        };
        return definition;
    }
}