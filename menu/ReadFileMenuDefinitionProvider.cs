using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Input;
using nfzf;

namespace nfm.menu;

public class ReadFileMenuDefinitionProvider(string path, IComparer<Entry>? comparer, string? searchString, MainViewModel viewModel) : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = (writer, ct) => ReverseFileReader.Read(path, writer),
            MinScore = 0,
            ResultHandler = new StdOutResultHandler(viewModel),
            QuitOnEscape = true,
            Comparer = comparer,
            FinalComparer = comparer,
            SearchString = searchString,
            ScoreFunc = (sObj, pattern, slab) =>
            {
                var s = (string)sObj;
                var score = FuzzySearcher.GetScore(s, pattern, slab);
                return (s.Length, score);
            },
        };
        return definition;
    }
}
