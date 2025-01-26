using System.Threading.Tasks;

namespace nfm.menu;

public interface IResultHandler
{
    Task HandleAsync(object output);
}
