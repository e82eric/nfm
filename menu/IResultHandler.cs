using System.Threading.Tasks;

namespace nfm.menu;

public interface IResultHandler<T>
{
    Task HandleAsync(T output, MainViewModel<T> viewModel);
}
