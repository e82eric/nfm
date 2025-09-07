namespace nfm.Ui.Core;

public interface IResultHandler
{
    Task HandleAsync(object output);
}
