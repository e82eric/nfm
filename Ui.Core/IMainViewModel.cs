using System.Threading.Tasks;

namespace nfm.menu;

public interface IMainViewModel
{
    Task RunDefinitionAsync(MenuDefinition definition);
    Task ShowToast(string message, int duration = 3000);
    Task Clear();
    Task Close();
}