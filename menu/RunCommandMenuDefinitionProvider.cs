namespace nfm.menu;

public class RunCommandMenuDefinitionProvider(string command) : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = (writer, ct) => ProcessRunner.RunCommand(command, writer),
            MinScore = 0,
            ResultHandler = new StdOutResultHandler(),
            ShowHeader = true
        };
        return definition;
    }
}