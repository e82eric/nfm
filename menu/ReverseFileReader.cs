using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using PSFzf.IO;

namespace nfm.menu;

public class ReverseFileReader
{
    public static async Task Read(string path, ChannelWriter<string> writer)
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