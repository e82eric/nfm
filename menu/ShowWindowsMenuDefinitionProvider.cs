using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Input;
using nfzf;

namespace nfm.menu;

public class ShowWindowsMenuDefinitionProvider2(IResultHandler resultHandler, Action? onClosed) : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = ListWindows.Run,
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Func<object, Task>>(),
            ResultHandler = resultHandler,
            MinScore = 0,
            ShowHeader = false,
            OnClosed = onClosed,
            ScoreFunc = (sObj, pattern, slab) =>
            {
                var s = (string)sObj;
                var score = FuzzySearcher.GetScore(s, pattern, slab);
                return (s.Length, score);
            },
            Comparer = Comparers.StringScoreLengthAndValue,
            FinalComparer = Comparers.StringScoreLengthAndValue,
        };
        return definition;
    }
}
