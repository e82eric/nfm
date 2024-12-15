using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using PSFzf.IO;

namespace nfm.menu;

public static class ReverseFileReader
{
    public static async Task Read(string path, ChannelWriter<object> writer)
    {
        var alreadyAdded = new HashSet<string>();
        var reader = new ReverseLineReader(path);
        foreach (var line in reader)
        {
            if (!alreadyAdded.Contains(line))
            {
                await writer.WriteAsync(line);
                alreadyAdded.Add(line);
            }
        }
        writer.Complete();
    }
}
