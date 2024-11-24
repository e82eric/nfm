using System;
using System.Collections.Generic;
using Avalonia.Input;
using nfzf.ListProcesses;

namespace nfm.menu;

public class ShowProcessesMenuDefinitionProvider : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var header = string.Format("{0,-75} {1,8} {2,20} {3,20} {4,10}",
            "Name", "PID", "WorkingSet(kb)", "PrivateBytes(kb)", "CPU(s)");
        var keyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>();
        keyBindings.Add((KeyModifiers.Control, Key.K), ProcessLister.KillProcessById);
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