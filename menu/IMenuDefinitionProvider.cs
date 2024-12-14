namespace nfm.menu;

public interface IMenuDefinitionProvider<T> where T:class
{
    MenuDefinition<T> Get();
}
