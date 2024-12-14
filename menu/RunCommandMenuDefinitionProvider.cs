using nfzf;

namespace nfm.menu;

public class RunCommandMenuDefinitionProvider(string command) : IMenuDefinitionProvider<string>
{
    public MenuDefinition<string> Get()
    {
        var definition = new MenuDefinition<string>
        {
            AsyncFunction = (writer, ct) => ProcessRunner.RunCommand(command, writer),
            MinScore = 0,
            ResultHandler = new StdOutResultHandler(),
            ShowHeader = true,
            ScoreFunc = (s, pattern, slab) =>
            {
                var score = FuzzySearcher.GetScore(s, pattern, slab);
                return (s.Length, score);
            },
            StrConverter = new StringConverter(),
            Comparer = Comparers.StringScoreLengthAndValue
        };
        return definition;
    }
}