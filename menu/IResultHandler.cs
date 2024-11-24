using System.Threading.Tasks;

namespace nfm.menu;

public interface IResultHandler
{
    Task HandleAsync(string output, MainViewModel viewModel);
}