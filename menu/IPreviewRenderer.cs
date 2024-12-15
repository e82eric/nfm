using Avalonia.Media.Imaging;

namespace nfm.menu;

public interface IPreviewRenderer
{
    public void RenderImage(Bitmap bitmap);
    public void RenderText(string info, string fileExtension);
    public void RenderError(string errorInfo);
}