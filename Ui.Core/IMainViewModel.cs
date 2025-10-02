namespace nfm.Ui.Core;

public interface IMainViewModel
{
    Task RunDefinitionAsync(MenuDefinition definition);
    Task ShowToast(string message, int duration = 3000);
    Task Clear();
    Task Close(bool quit);
    List<object> GetAllCurrentSearchResults();
    void UpdateHeader();
}