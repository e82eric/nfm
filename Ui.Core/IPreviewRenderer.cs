namespace nfm.menu;

public interface IPreviewRenderer
{
    public void RenderImage(MemoryStream bitmap);
    public void RenderText(List<string> lines, string fileExtension);
    public void RenderError(string errorInfo);
}