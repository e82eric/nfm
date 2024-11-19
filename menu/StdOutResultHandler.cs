using System;

namespace nfm.menu;

public class StdOutResultHandler : IResultHandler
{
    public void Handle(string output)
    {
        Console.WriteLine(output);
        Environment.Exit(0);
    }
}