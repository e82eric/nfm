using System.Threading.Tasks;

namespace nfm.menu;

public interface IResultHandler
{
    void Handle(string output);
    Task HandleAsync(string output);
}