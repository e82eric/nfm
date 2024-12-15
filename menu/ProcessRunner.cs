using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace nfm.menu;

public static class ProcessRunner
{
    public static async Task RunCommand(string command, ChannelWriter<object> writer)
    {
        using (var process = new Process())
        {
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = $"/C {command}";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();

            using (var stream = process.StandardOutput.BaseStream)
            {
                using (var reader = new StreamReader(stream))
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (!string.IsNullOrEmpty(line) && line.Length > 3)
                        {
                            await writer.WriteAsync(line);
                        }
                    }
                }
            }
            await process.WaitForExitAsync();
            writer.Complete();
        }
    }
}
