using System.Threading.Channels;
using nfm.PowershellHistoryReader;

var historyPath =
    (args.Length > 0 ? args[0] : null)
    ?? Path.Combine(
           Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
           "Microsoft", "Windows", "PowerShell", "PSReadLine",
           "ConsoleHost_history.txt");

if (!File.Exists(historyPath))
{
    Console.Error.WriteLine($"History file not found: {historyPath}");
    return;
}

var channel = Channel.CreateUnbounded<object>();
_ = Task.Run(() => ReverseFileReader.Read(historyPath, channel.Writer));

await foreach (var item in channel.Reader.ReadAllAsync())
{
    Console.WriteLine(item);
}
