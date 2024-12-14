using System;
using System.Threading.Tasks;
using nfzf.FileSystem;

namespace nfm.menu;

public class StdOutResultHandler : IResultHandler<string>, IResultHandler<FileSystemNode>
{
    private void Handle(string output)
    {
        Console.WriteLine(output);
        Environment.Exit(0);
    }

    public async Task HandleAsync(string output, MainViewModel<string> viewModel)
    {
        await viewModel.Close();
        Handle(output);
    }

    public async Task HandleAsync(FileSystemNode output, MainViewModel<FileSystemNode> viewModel)
    {
        await viewModel.Close();
        Handle(output.ToString());
    }
}
