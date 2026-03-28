namespace nfm.Ui.Core;

public interface IPreviewRenderer
{
    public void RenderImage(MemoryStream bitmap);
    public void RenderText(List<List<TextSegment>> lines, int startLine);
    public void RenderError(string errorInfo);
    public void RenderThumbnail(IntPtr hwnd);
}