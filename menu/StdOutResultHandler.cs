using System;
using System.Threading.Tasks;

namespace nfm.menu;

public class StdOutResultHandler : IResultHandler
{
    private void Handle(string output)
    {
        Console.WriteLine(output);
        Environment.Exit(0);
    }

    public async Task HandleAsync(object output, MainViewModel viewModel)
    {
        await viewModel.Close();
        Handle(output.ToString());
    }
}
