using System.Threading.Channels;
using Core;
using PSFzf.IO;

namespace nfm.menu;

public static class ReverseFileReader
{
    public static async Task Read(string path, ChannelWriter<object> writer)
    {
        var alreadyAdded = new HashSet<string>();
        var reader = new ReverseLineReader(path);

        TerminalEscapedLine? currentLine = null;
        foreach (var line in reader)
        {
            if (currentLine == null || line.EndsWith("`"))
            {
                if (currentLine == null)
                {
                    currentLine = new TerminalEscapedLine();
                }
                var segments = new List<TextSegment> { new() {State = new AnsiState(), Text = line} };
                currentLine.Lines.Insert(0, new EscapedLine(segments));
            }
            else
            {
                await SendToWriter(currentLine, writer, alreadyAdded);
                currentLine = new TerminalEscapedLine();
                var segments = new List<TextSegment> { new() {State = new AnsiState(), Text = line} };
                currentLine.Lines.Add(new EscapedLine(segments));
            }
        }

        if (currentLine != null)
        {
            await SendToWriter(currentLine, writer, alreadyAdded);
        }
        writer.Complete();
    }

    private static async Task SendToWriter(TerminalEscapedLine line, ChannelWriter<object> writer, HashSet<string> alreadyAdded)
    {
        var combinedLine = line.ToString();
        if (!alreadyAdded.Contains(combinedLine))
        {
            await writer.WriteAsync(line);
            alreadyAdded.Add(combinedLine);
        }
    }
}
