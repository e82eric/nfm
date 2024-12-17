namespace nfm.menu;

public class StdOutResultHandler(IMainViewModel viewModel) : IResultHandler
{
    private void Handle(string output)
    {
        Console.WriteLine(output);
        Environment.Exit(0);
    }

    public async Task HandleAsync(object output)
    {
        await viewModel.Close();
        Handle(output.ToString());
    }
}
