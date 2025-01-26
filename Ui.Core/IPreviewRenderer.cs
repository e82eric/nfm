namespace nfm.menu;

public interface IPreviewRenderer
{
    public void RenderImage(MemoryStream bitmap);
    public void RenderText(string info, string fileExtension);
    public void RenderError(string errorInfo);
}