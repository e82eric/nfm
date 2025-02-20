namespace nfm.menu;

public class StdOutResultHandler(IMainViewModel viewModel) : IResultHandler
{
    private void Handle(string output)
    {
        Console.WriteLine(output);
    }

    public async Task HandleAsync(object output)
    {
        var outputStr = output.ToString();
        if (outputStr == null) return;
        Handle(outputStr);
        //Environment.Exit(0);
        await viewModel.Close();
        //await Task.Delay(TimeSpan.FromMilliseconds(200));
        //Environment.Exit(0);
    }
}
