using nfzf;

namespace nfm.menu;

public class RunCommandMenuDefinitionProvider(string command, MainViewModel viewModel) : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = (writer, ct) => ProcessRunner.RunCommand(command, writer),
            MinScore = 0,
            ResultHandler = new StdOutResultHandler(viewModel),
            ScoreFunc = (sObj, pattern, slab) =>
            {
                var s = (string)sObj;
                var score = FuzzySearcher.GetScore(s, pattern, slab);
                return (s.Length, score);
            },
            Comparer = Comparers.ScoreLengthAndValue
        };
        return definition;
    }
}
