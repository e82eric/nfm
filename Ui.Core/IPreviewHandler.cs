namespace nfm.menu;

public interface IPreviewHandler
{
    Task Handle(IPreviewRenderer renderer, object t, CancellationToken ct);
}