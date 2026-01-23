namespace nfm.Ui.Core;

public class StdOutResultHandler(IMainViewModel viewModel) : IResultHandler
{
    private void Handle(string output)
    {
        Console.WriteLine(output);
        Console.Out.Flush();
    }

    public async Task HandleAsync(object output)
    {
        var outputStr = output.ToString();
        if (outputStr == null) return;
        Handle(outputStr);
        Environment.Exit(0);
        //await viewModel.Close(true);
    }
}
