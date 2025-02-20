using System.Threading.Channels;
using nfm.menu;

var channel = Channel.CreateUnbounded<object>();
await ReverseFileReader.Read(
    @"C:\Users\eric\AppData\Roaming\Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt",
    channel.Writer);

await foreach (var item in channel.Reader.ReadAllAsync())
{
    Console.WriteLine(item);
}