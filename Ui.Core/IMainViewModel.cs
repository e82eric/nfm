using System.Threading.Tasks;

namespace nfm.menu;

public interface IMainViewModel
{
    public void TogglePreview();
    Task RunDefinitionAsync(MenuDefinition definition);
    Task ShowToast(string message, int duration = 3000);
    Task Clear();
    Task Close(bool quit);
}