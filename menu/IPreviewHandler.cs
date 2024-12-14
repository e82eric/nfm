using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace nfm.menu;

public interface IPreviewHandler<in T>
{
    Task Handle(IPreviewRenderer renderer, T t, CancellationToken ct);
}

public interface IPreviewRenderer
{
    public void RenderImage(Bitmap bitmap);
    public void RenderText(string info, string fileExtension);
    public void RenderError(string errorInfo);
}