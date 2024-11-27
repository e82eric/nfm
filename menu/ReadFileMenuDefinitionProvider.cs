using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Input;

namespace nfm.menu;

public class ReadFileMenuDefinitionProvider(string path, IComparer<Entry>? comparer) : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = (writer, ct) => ReverseFileReader.Read(path, writer),
            MinScore = 0,
            ResultHandler = new StdOutResultHandler(),
            KeyBindings = new Dictionary<(KeyModifiers, Key), Func<string, Task>>(),
            ShowHeader = true,
            QuitOnEscape = true,
            Comparer = comparer
        };
        return definition;
    }
}