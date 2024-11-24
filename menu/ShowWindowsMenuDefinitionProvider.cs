using System;
using System.Collections.Generic;
using Avalonia.Input;

namespace nfm.menu;

public class ShowWindowsMenuDefinitionProvider : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = ListWindows.Run,
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
            ResultHandler = new StdOutResultHandler(),
            MinScore = 0,
            ShowHeader = false
        };
        return definition;
    }
}