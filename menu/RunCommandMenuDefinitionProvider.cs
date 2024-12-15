using nfzf;

namespace nfm.menu;

public class RunCommandMenuDefinitionProvider2(string command) : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = (writer, ct) => ProcessRunner.RunCommand(command, writer),
            MinScore = 0,
            ResultHandler = new StdOutResultHandler(),
            ShowHeader = true,
            ScoreFunc = (sObj, pattern, slab) =>
            {
                var s = (string)sObj;
                var score = FuzzySearcher.GetScore(s, pattern, slab);
                return (s.Length, score);
            },
            Comparer = Comparers.StringScoreLengthAndValue
        };
        return definition;
    }
}
