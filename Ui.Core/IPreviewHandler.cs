namespace nfm.Ui.Core;

public interface IPreviewHandler
{
    Task Handle(IPreviewRenderer renderer, object t, int height, CancellationToken ct);
}