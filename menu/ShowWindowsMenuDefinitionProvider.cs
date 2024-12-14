using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Input;
using nfzf;

namespace nfm.menu;

public class ShowWindowsMenuDefinitionProvider(IResultHandler<string> resultHandler, Action? onClosed) : IMenuDefinitionProvider<string>
{
    public MenuDefinition<string> Get()
    {
        var definition = new MenuDefinition<string>
        {
            AsyncFunction = ListWindows.Run,
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Func<string, Task>>(),
            ResultHandler = resultHandler,
            MinScore = 0,
            ShowHeader = false,
            OnClosed = onClosed,
            ScoreFunc = (s, pattern, slab) =>
            {
                var score = FuzzySearcher.GetScore(s, pattern, slab);
                return (s.Length, score);
            },
            Comparer = Comparers.StringScoreLengthAndValue,
            StrConverter = new StringConverter()
        };
        return definition;
    }
}