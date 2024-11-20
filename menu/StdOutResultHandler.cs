using System;
using System.Threading.Tasks;

namespace nfm.menu;

public class StdOutResultHandler : IResultHandler
{
    public void Handle(string output)
    {
        Console.WriteLine(output);
        Environment.Exit(0);
    }

    public Task HandleAsync(string output)
    {
        Handle(output);
        return Task.CompletedTask;
    }
}