using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Input;
using nfzf;

namespace nfm.menu;

public class ReadFileMenuDefinitionProvider2(string path, IComparer<Entry<string>>? comparer, string? searchString) : IMenuDefinitionProvider<string>
{
    public MenuDefinition<string> Get()
    {
        var definition = new MenuDefinition<string>
        {
            AsyncFunction = (writer, ct) => ReverseFileReader.Read(path, writer),
            MinScore = 0,
            ResultHandler = new StdOutResultHandler(),
            KeyBindings = new Dictionary<(KeyModifiers, Key), Func<string, Task>>(),
            ShowHeader = true,
            QuitOnEscape = true,
            Comparer = comparer,
            SearchString = searchString,
            ScoreFunc = (s, pattern, slab) =>
            {
                var score = FuzzySearcher.GetScore(s, pattern, slab);
                return (s.Length, score);
            },
            StrConverter = new StringConverter()
        };
        return definition;
    }
}
