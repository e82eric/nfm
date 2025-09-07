using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

namespace nfm.Ui.Core;

public static class ProcessRunner
{
    public static async Task RunCommand(string command, ChannelWriter<object> writer)
    {
        using (var process = new Process())
        {
#if WINDOWS
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = $"/C {command}";
#endif
#if LINUX
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = $"-c \"{command}\"";
#endif
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
    
    public class CommandResult
    {
        public required List<string> StandardOutput { get; init; }
        public required List<string> StandardError { get; init; }
        public int ExitCode { get; set; }
    }

    public static async Task<CommandResult> RunCommandAsync(string command)
    {
        using (var process = new Process())
        {
#if WINDOWS
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = $"/C {command}";
#endif
#if LINUX
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = $"-c \"{command}\"";
#endif
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            process.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();

            var standardOutput = new List<string>();
            var standardError = new List<string>();

            process.OutputDataReceived += (sender, args) =>
            {
                standardOutput.Add(args.Data ?? string.Empty);
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    standardError.Add(args.Data);
                }
            };

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            return new CommandResult
            {
                StandardOutput = standardOutput,
                StandardError = standardError,
                ExitCode = process.ExitCode
            };
        }
    }
}
