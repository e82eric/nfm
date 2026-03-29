using System.Globalization;
using nfm.Ui.Core;

namespace nfm.ListWindows;

public class WindowPreviewHandler : IPreviewHandler
{
    public Task Handle(IPreviewRenderer renderer, object t, int height, CancellationToken ct)
    {
        if (t is ListWindows.ListWindowsItem lwi)
        {
            renderer.RenderThumbnail(lwi.Hwnd);
            return Task.CompletedTask;
        }

        var line = t.ToString();
        if (line == null || line.Length < 8)
            return Task.CompletedTask;

        var hwndHex = line.AsSpan(0, 8).Trim();
        if (long.TryParse(hwndHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hwndValue))
        {
            renderer.RenderThumbnail(new IntPtr(hwndValue));
        }

        return Task.CompletedTask;
    }
}
