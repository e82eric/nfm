using System;
using System.Threading.Tasks;

namespace nfm.menu;

public class StdOutResultHandler(MainViewModel viewModel) : IResultHandler
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
