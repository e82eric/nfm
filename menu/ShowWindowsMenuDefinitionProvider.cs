using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Input;

namespace nfm.menu;

public class ShowWindowsMenuDefinitionProvider(IResultHandler resultHandler, Action? onClosed) : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = ListWindows.Run,
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Func<string, Task>>(),
            ResultHandler = resultHandler,
            MinScore = 0,
            ShowHeader = false,
            OnClosed = onClosed,
            Title = "Windows"
        };
        return definition;
    }
}