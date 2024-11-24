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

    public Task HandleAsync(string output, MainViewModel viewModel)
    {
        viewModel.Close();
        Handle(output);
        return Task.CompletedTask;
    }
}